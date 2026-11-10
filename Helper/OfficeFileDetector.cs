using System.Buffers.Binary;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace MimeTypeDetection.Helper;

internal static class OfficeFileDetector
{
    internal const int MaxEntries = 2048;
    internal const int MaxDirectoryBytes = 1024 * 1024;
    internal const int MaxManifestBytes = 64 * 1024;
    internal const long MaxExpandedBytes = 200L * 1024 * 1024;

    internal static DetectionResult Detect(string? fileName, byte[] content)
    {
        try
        {
            // Preflight the central directory BEFORE ZipArchive allocates entry objects.
            CheckDirectory(content);
            using var stream = new MemoryStream(content, writable: false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            if (archive.Entries.Count > MaxEntries) throw new ArchiveLimitException();
            long total = 0;
            var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
            foreach (var entry in archive.Entries)
            {
                if (entry.Length > MaxExpandedBytes - total) throw new ArchiveLimitException();
                total += entry.Length;
                if (!entries.TryAdd(entry.FullName, entry)) throw new InvalidDataException();
            }

            if (!entries.TryGetValue("[Content_Types].xml", out var manifest))
                return MimeTypeService.Found(fileName, ".zip", "application/zip", "Container");

            var document = ReadXml(manifest);
            XNamespace ns = "http://schemas.openxmlformats.org/package/2006/content-types";
            if (document.Root?.Name != ns + "Types") throw new InvalidDataException();
            // Require a package-level Office relationship as well as a content-type declaration.
            // Directory names alone (word/, xl/, ppt/) are not evidence of Office content.
            var mainParts = new HashSet<string>(StringComparer.Ordinal);
            if (entries.TryGetValue("_rels/.rels", out var relationships))
            {
                var rels = ReadXml(relationships);
                XNamespace relNs = "http://schemas.openxmlformats.org/package/2006/relationships";
                if (rels.Root?.Name != relNs + "Relationships") throw new InvalidDataException();
                foreach (var rel in rels.Root.Elements(relNs + "Relationship"))
                {
                    string type = (string?)rel.Attribute("Type") ?? "";
                    string target = (string?)rel.Attribute("Target") ?? "";
                    if ((type == "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" ||
                         type == "http://purl.oclc.org/ooxml/officeDocument/relationships/officeDocument") &&
                        (string?)rel.Attribute("TargetMode") != "External")
                        mainParts.Add(target.TrimStart('/'));
                }
            }
            var candidates = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in document.Root.Elements(ns + "Override"))
            {
                string part = (string?)item.Attribute("PartName") ?? "";
                string type = (string?)item.Attribute("ContentType") ?? "";
                string extension = type switch
                {
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml" => ".docx",
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml" => ".xlsx",
                    "application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml" => ".pptx",
                    _ => ""
                };
                if (extension != "" && part.StartsWith('/') && mainParts.Contains(part[1..]) &&
                    entries.TryGetValue(part[1..], out var main) && main.Length > 0)
                    candidates.Add(extension);
            }
            if (candidates.Count > 1)
                return MimeTypeService.Failure("AMBIGUOUS_FORMAT", "Archive declares multiple Office document types.", "Container", "application/zip");
            if (candidates.Count == 1)
            {
                string extension = candidates.Single();
                // Do not label macro-containing packages as macro-free Office formats.
                if (!entries.Keys.Any(n => n.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase)))
                    return MimeTypeService.Found(fileName, extension, MimeTypeMap.Mappings[extension], "Container");
            }
            return MimeTypeService.Found(fileName, ".zip", "application/zip", "Container");
        }
        catch (ArchiveLimitException)
        {
            return MimeTypeService.Failure("ARCHIVE_LIMIT_EXCEEDED", "Archive exceeds inspection limits.", "Container");
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or XmlException or NotSupportedException or ArgumentException or OverflowException)
        {
            return MimeTypeService.Failure("INVALID_ARCHIVE", "Archive is malformed or uses an unsupported ZIP feature.", "Container");
        }
    }

    private static XDocument ReadXml(ZipArchiveEntry entry)
    {
        if (entry.Length > MaxManifestBytes) throw new ArchiveLimitException();
        // Read at most limit+1 bytes even if the declared length is false.
        using var source = entry.Open();
        using var buffer = new MemoryStream();
        byte[] chunk = new byte[4096];
        int read;
        while ((read = source.Read(chunk, 0, Math.Min(chunk.Length, MaxManifestBytes + 1 - (int)buffer.Length))) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaxManifestBytes) throw new ArchiveLimitException();
        }
        if (buffer.Length != entry.Length) throw new InvalidDataException();
        buffer.Position = 0;
        using var reader = XmlReader.Create(buffer, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
            MaxCharactersInDocument = MaxManifestBytes
        });
        return XDocument.Load(reader);
    }

    private static void CheckDirectory(byte[] bytes)
    {
        int end = -1;
        for (int i = bytes.Length - 22; i >= Math.Max(0, bytes.Length - 22 - ushort.MaxValue); i--)
        {
            if (U32(bytes, i) == 0x06054b50 && i + 22 + U16(bytes, i + 20) == bytes.Length)
            {
                end = i;
                break;
            }
        }
        if (end < 0) throw new InvalidDataException();
        int count = U16(bytes, end + 10);
        uint size = U32(bytes, end + 12), offset = U32(bytes, end + 16);
        if (count > MaxEntries || size > MaxDirectoryBytes) throw new ArchiveLimitException();
        // No multi-volume/ZIP64 archives. Bound both metadata and entry allocations.
        if (U16(bytes, end + 4) != 0 || U16(bytes, end + 6) != 0 || U16(bytes, end + 8) != count ||
            (long)offset + size != end) throw new InvalidDataException();
        int position = checked((int)offset);
        for (int i = 0; i < count; i++)
        {
            if (position > end - 46 || U32(bytes, position) != 0x02014b50) throw new InvalidDataException();
            if (U16(bytes, position + 34) != 0) throw new InvalidDataException();
            int flags = U16(bytes, position + 8);
            int method = U16(bytes, position + 10);
            if ((flags & 0x41) != 0 || (method != 0 && method != 8)) throw new InvalidDataException();
            uint local = U32(bytes, position + 42);
            if (local + 30L > offset || U32(bytes, (int)local) != 0x04034b50)
                throw new InvalidDataException();
            int localOffset = (int)local;
            int nameLength = U16(bytes, position + 28);
            int next = position + 46 + nameLength + U16(bytes, position + 30) + U16(bytes, position + 32);
            if (next > end || U16(bytes, localOffset + 6) != flags || U16(bytes, localOffset + 8) != method ||
                U16(bytes, localOffset + 26) != nameLength) throw new InvalidDataException();
            long dataEnd = local + 30L + nameLength + U16(bytes, localOffset + 28) + U32(bytes, position + 20);
            if (dataEnd > offset) throw new InvalidDataException();
            if (!bytes.AsSpan(localOffset + 30, nameLength).SequenceEqual(bytes.AsSpan(position + 46, nameLength)))
                throw new InvalidDataException();
            if ((flags & 8) == 0 && (U32(bytes, localOffset + 14) != U32(bytes, position + 16) ||
                U32(bytes, localOffset + 18) != U32(bytes, position + 20) ||
                U32(bytes, localOffset + 22) != U32(bytes, position + 24))) throw new InvalidDataException();
            position = next;
        }
        if (position != end) throw new InvalidDataException();
    }

    private static ushort U16(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
    private static uint U32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    private sealed class ArchiveLimitException : Exception { }
}
