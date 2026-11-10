using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Xunit;

namespace MimeTypeDetection.Tests;

public class DetectionTests
{
    private readonly MimeTypeService service = new();

    [Theory]
    [InlineData("89504E470D0A1A0A", ".png", "image/png")]
    [InlineData("FFD8FF", ".jpg", "image/jpeg")]
    [InlineData("474946383761", ".gif", "image/gif")]
    [InlineData("474946383961", ".gif", "image/gif")]
    [InlineData("49492A00", ".tif", "image/tiff")]
    [InlineData("4D4D002A", ".tif", "image/tiff")]
    [InlineData("524946460000000057415645", ".wav", "audio/wav")]
    [InlineData("524946460000000041564920", ".avi", "video/x-msvideo")]
    [InlineData("524946460000000057454250", ".webp", "image/webp")]
    public void DetectsBytesRegardlessOfName(string hex, string extension, string mime)
    {
        var result = service.Detect("renamed.exe", Convert.FromHexString(hex));
        Assert.True(result.IsRecognized);
        Assert.True(result.ExtensionMismatch);
        Assert.Equal(extension, result.DetectedExtension);
        Assert.Equal(mime, result.MimeType);
        Assert.Equal("", result.ErrorCode);
    }

    [Theory]
    [InlineData("photo.JPEG")]
    [InlineData("C:\\uploads\\photo.jpg")]
    [InlineData("/uploads/photo.jpe")]
    public void AcceptsExtensionAliases(string name) =>
        Assert.False(service.Detect(name, Convert.FromHexString("FFD8FF")).ExtensionMismatch);

    [Theory]
    [InlineData(0, true)]
    [InlineData(1019, true)]
    [InlineData(1020, false)]
    public void PdfSearchIsBounded(int offset, bool recognized)
    {
        byte[] content = new byte[offset + 5];
        Encoding.ASCII.GetBytes("%PDF-").CopyTo(content, offset);
        Assert.Equal(recognized, service.Detect("file.pdf", content).IsRecognized);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(256, false)]
    [InlineData(257, true)]
    [InlineData(258, false)]
    public void TarUsesFixedOffset(int offset, bool recognized)
    {
        byte[] content = new byte[300];
        Encoding.ASCII.GetBytes("ustar\0").CopyTo(content, offset);
        Assert.Equal(recognized, service.Detect("file.tar", content).IsRecognized);
    }

    [Theory]
    [InlineData(".doc", "word/document.xml", "wordprocessingml.document", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData(".xls", "xl/workbook.xml", "spreadsheetml.sheet", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData(".ppt", "ppt/presentation.xml", "presentationml.presentation", "application/vnd.openxmlformats-officedocument.presentationml.presentation")]
    public void OfficeUsesInMemoryManifestNotName(string oldExtension, string part, string contentType, string mime)
    {
        byte[] content = Office(part, contentType);
        var result = service.Detect("/does/not/exist/file" + oldExtension, content);
        Assert.True(result.IsRecognized);
        Assert.Equal(oldExtension + "x", result.DetectedExtension);
        Assert.Equal(mime, result.MimeType);
        Assert.True(result.ExtensionMismatch);
        Assert.Equal("Container", result.DetectionMethod);
        Assert.Equal(result.MimeType, service.GetFileType("file.zip", content).MimeType);
    }

    [Fact]
    public void ZipWithWordFolderIsNotOffice()
    {
        var result = service.Detect("fake.docx", Zip(("word/document.xml", "not office")));
        Assert.Equal(".zip", result.DetectedExtension);
        Assert.True(result.ExtensionMismatch);
    }

    [Fact]
    public void EmptyZipIsRecognized() => Assert.Equal(".zip", service.Detect("a.zip", Zip()).DetectedExtension);

    [Theory]
    [InlineData("doc")]
    [InlineData("xls")]
    [InlineData("ppt")]
    [InlineData("msg")]
    public void OleDoesNotGuessSubtype(string extension)
    {
        var result = service.Detect("file." + extension, Convert.FromHexString("D0CF11E0A1B11AE1"));
        Assert.False(result.IsRecognized);
        Assert.Equal("", result.DetectedExtension);
        Assert.Equal("application/x-ole-storage", result.MimeType);
        Assert.Equal("AMBIGUOUS_FORMAT", result.ErrorCode);
    }

    [Theory]
    [InlineData("89504E47")]
    [InlineData("47494638")]
    [InlineData("78")]
    [InlineData("4344303031")]
    [InlineData("4D5A")]
    [InlineData("0001020304")]
    public void WeakOrTruncatedSignaturesDoNotMatch(string hex)
    {
        var result = service.Detect("trusted.pdf", Convert.FromHexString(hex));
        Assert.False(result.IsRecognized);
        Assert.Equal("application/octet-stream", result.MimeType);
        Assert.Equal("UNSUPPORTED_FORMAT", result.ErrorCode);
    }

    [Fact]
    public void UnknownRiffIsAmbiguous() => Assert.Equal("AMBIGUOUS_FORMAT", service.Detect("a.avi", Encoding.ASCII.GetBytes("RIFFxxxx????")).ErrorCode);

    [Fact]
    public void TruncatedRiffHasStableError() => Assert.Equal("TRUNCATED_CONTENT", service.Detect("a.wav", Encoding.ASCII.GetBytes("RIFF")).ErrorCode);

    [Fact]
    public void EmptyContentNeverFallsBackToExtension()
    {
        foreach (var bytes in new byte[]?[] { null, Array.Empty<byte>() })
        {
            var result = service.Detect("a.pdf", bytes);
            Assert.Equal("EMPTY_CONTENT", result.ErrorCode);
            Assert.False(result.IsRecognized);
            Assert.Equal("", result.DetectedExtension);
        }
        Assert.Equal("application/octet-stream", service.GetFileType("a.pdf").MimeType);
    }

    [Fact]
    public void UnsupportedContentNeverFallsBackToExtension() =>
        Assert.Equal("UNSUPPORTED_FORMAT", service.Detect("a.pdf", Encoding.UTF8.GetBytes("not a PDF")).ErrorCode);

    [Fact]
    public void NullFilenameIsMetadataOnly() => Assert.True(service.Detect(null, Convert.FromHexString("FFD8FF")).IsRecognized);

    [Fact]
    public void OversizedContentIsRejectedBeforeInspection() =>
        Assert.Equal("FILE_TOO_LARGE", service.Detect("a.bin", new byte[MimeTypeService.MaxContentBytes + 1]).ErrorCode);

    [Fact]
    public void MalformedZipReturnsSafeError()
    {
        var result = service.Detect("/secret/path.docx", Convert.FromHexString("504B0304"));
        Assert.Equal("INVALID_ARCHIVE", result.ErrorCode);
        Assert.False(result.IsRecognized);
        Assert.DoesNotContain("secret", result.ErrorMessage);
    }

    [Fact]
    public void TruncatedZipIsRejected()
    {
        byte[] content = Zip(("file", "hello"));
        Assert.Equal("INVALID_ARCHIVE", service.Detect("a.zip", content[..^1]).ErrorCode);
    }

    [Fact]
    public void ManifestHasBoundedDecompression() =>
        Assert.Equal("ARCHIVE_LIMIT_EXCEEDED", service.Detect("a.zip", Zip(("[Content_Types].xml", new string('x', 65537)))).ErrorCode);

    [Fact]
    public void EntryCountIsBounded() =>
        Assert.Equal("ARCHIVE_LIMIT_EXCEEDED", service.Detect("a.zip", Zip(Enumerable.Range(0, 2049).Select(i => (i.ToString(), "")).ToArray())).ErrorCode);

    [Fact]
    public void ExpandedSizeIsBoundedWithoutExtracting()
    {
        byte[] content = Zip(("file", "hello"));
        int directory = (int)BinaryPrimitives.ReadUInt32LittleEndian(content.AsSpan(content.Length - 6, 4));
        BinaryPrimitives.WriteUInt32LittleEndian(content.AsSpan(directory + 24, 4), 201 * 1024 * 1024);
        BinaryPrimitives.WriteUInt32LittleEndian(content.AsSpan(22, 4), 201 * 1024 * 1024);
        Assert.Equal("ARCHIVE_LIMIT_EXCEEDED", service.Detect("a.zip", content).ErrorCode);
    }

    [Fact]
    public void XmlDtdIsProhibited()
    {
        string xml = "<!DOCTYPE Types [<!ENTITY x SYSTEM 'file:///secret'>]><Types>&x;</Types>";
        Assert.Equal("INVALID_ARCHIVE", service.Detect("a.docx", Zip(("[Content_Types].xml", xml))).ErrorCode);
    }

    [Fact]
    public void DuplicateEntriesAreRejected() =>
        Assert.Equal("INVALID_ARCHIVE", service.Detect("a.zip", Zip(("file", "one"), ("file", "two"))).ErrorCode);

    internal static byte[] Office(string part = "word/document.xml", string type = "wordprocessingml.document") => Zip(
        ("[Content_Types].xml", $"<Types xmlns='http://schemas.openxmlformats.org/package/2006/content-types'><Override PartName='/{part}' ContentType='application/vnd.openxmlformats-officedocument.{type}.main+xml'/></Types>"),
        ("_rels/.rels", $"<Relationships xmlns='http://schemas.openxmlformats.org/package/2006/relationships'><Relationship Id='rId1' Type='http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument' Target='{part}'/></Relationships>"),
        (part, "<document/>"));

    internal static byte[] Zip(params (string Name, string Text)[] files)
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, true))
            foreach (var file in files)
            {
                var entry = archive.CreateEntry(file.Name);
                entry.LastWriteTime = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
                using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                writer.Write(file.Text);
            }
        return memory.ToArray();
    }
}
