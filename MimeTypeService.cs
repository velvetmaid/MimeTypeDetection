using System.Text;
using MimeTypeDetection.Helper;

namespace MimeTypeDetection
{
    public class MimeTypeService
    {
        public const int MaxContentBytes = 50 * 1024 * 1024;

        // Keep only signatures that identify a format rather than a shared container.
        private static readonly FileType[] KnownTypes =
        {
            new("PNG", ".png", new byte?[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            new("JPEG", ".jpg", new byte?[] { 255, 216, 255 }),
            new("GIF87a", ".gif", Encoding.ASCII.GetBytes("GIF87a").Select(b => (byte?)b).ToArray()),
            new("GIF89a", ".gif", Encoding.ASCII.GetBytes("GIF89a").Select(b => (byte?)b).ToArray()),
            new("Bitmap", ".bmp", new byte?[] { 66, 77 }),
            new("ICO", ".ico", new byte?[] { 0, 0, 1, 0 }),
            new("TIFF", ".tif", new byte?[] { 73, 73, 42, 0 }),
            new("TIFF", ".tif", new byte?[] { 77, 77, 0, 42 }),
            new("TTF", ".ttf", new byte?[] { 0, 1, 0, 0, 0 }),
            new("PDF", ".pdf", new byte?[] { 37, 80, 68, 70, 45 }, 0, 1019),
            new("RAR4", ".rar", new byte?[] { 82, 97, 114, 33, 26, 7, 0 }),
            new("RAR5", ".rar", new byte?[] { 82, 97, 114, 33, 26, 7, 1, 0 }),
            new("7-Zip", ".7z", new byte?[] { 55, 122, 188, 175, 39, 28 }),
            new("TAR", ".tar", new byte?[] { 117, 115, 116, 97, 114, 0 }, 257, 257),
            new("TAR GNU", ".tar", new byte?[] { 117, 115, 116, 97, 114, 32 }, 257, 257),
            new("Compress", ".z", new byte?[] { 31, 157 }),
            new("Bzip2", ".bz2", new byte?[] { 66, 90, 104 }),
            new("GZIP", ".gz", new byte?[] { 31, 139, 8 }),
            new("XAR", ".xar", new byte?[] { 120, 97, 114, 33 }),
            new("CAB", ".cab", new byte?[] { 77, 83, 67, 70 }),
            new("CHM", ".chm", new byte?[] { 73, 84, 83, 70 }),
            new("RPM", ".rpm", new byte?[] { 237, 171, 238, 219 }),
            new("SWF", ".swf", new byte?[] { 70, 87, 83 }),
            new("SWF compressed", ".swf", new byte?[] { 67, 87, 83 }),
            new("ARJ", ".arj", new byte?[] { 96, 234 }),
            new("LZH", ".lzh", new byte?[] { 45, 108, 104, null, 45 }, 2, 2),
            new("ISO9660", ".iso", new byte?[] { 1, 67, 68, 48, 48, 49, 1 }, 32768, 32768),
        };

        // Retained .NET API; extension-only inference has deliberately been removed.
        public FileTypeRecord GetFileType(string fileName, byte[]? content = null)
        {
            var result = Detect(fileName, content);
            return new FileTypeRecord(result.DetectedExtension, result.MimeType, result.ErrorMessage);
        }

        public DetectionResult Detect(string? fileName, byte[]? content)
        {
            if (content == null || content.Length == 0)
                return Failure("EMPTY_CONTENT", "File content is required.");
            if (content.Length > MaxContentBytes)
                return Failure("FILE_TOO_LARGE", "File exceeds the 50 MiB inspection limit.");

            if (Has(content, 0, new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }))
                return Failure("AMBIGUOUS_FORMAT", "OLE container detected; its document subtype is not supported.", "Container", "application/x-ole-storage");

            if (HasAscii(content, 0, "OggS") && content.Length >= 27 && content[4] == 0)
                return Found(fileName, ".ogg", "application/ogg", "Container");
            if (Has(content, 0, new byte[] { 48, 38, 178, 117, 142, 102, 207, 17, 166, 217, 0, 170, 0, 98, 206, 108 }))
                return Found(fileName, ".asf", "application/vnd.ms-asf", "Container");
            if (Has(content, 0, new byte[] { 26, 69, 223, 163 }))
                return Failure("AMBIGUOUS_FORMAT", "EBML container detected; Matroska/WebM subtype inspection is not supported.", "Container");
            if (content.Length >= 512 && HasAscii(content, content.Length - 512, "koly"))
                return Found(fileName, ".dmg", "application/x-apple-diskimage", "Signature");
            if (content.Length >= 512 && HasAscii(content, content.Length - 512, "conectix"))
                return Found(fileName, ".vhd", "application/x-vhd", "Signature");

            if (HasTwoMp3Frames(content)) return Found(fileName, ".mp3", "audio/mpeg", "Signature");

            if (HasAscii(content, 0, "RIFF"))
            {
                if (content.Length < 12) return Failure("TRUNCATED_CONTENT", "RIFF header is incomplete.");
                if (HasAscii(content, 8, "WAVE")) return Found(fileName, ".wav", "audio/wav", "Container");
                if (HasAscii(content, 8, "AVI ")) return Found(fileName, ".avi", "video/x-msvideo", "Container");
                if (HasAscii(content, 8, "WEBP")) return Found(fileName, ".webp", "image/webp", "Container");
                return Failure("AMBIGUOUS_FORMAT", "Unsupported RIFF subtype.", "Container");
            }

            if (Has(content, 0, new byte[] { 80, 75, 3, 4 }) ||
                Has(content, 0, new byte[] { 80, 75, 5, 6 }) ||
                Has(content, 0, new byte[] { 80, 75, 7, 8 }))
                return OfficeFileDetector.Detect(fileName, content);

            // MZ identifies DOS executables as well as PE files; require a PE header.
            if (HasAscii(content, 0, "MZ") && content.Length >= 64)
            {
                uint pe = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(content.AsSpan(60, 4));
                if (pe <= int.MaxValue && Has(content, (int)pe, new byte[] { 80, 69, 0, 0 }))
                    return Found(fileName, ".exe", "application/vnd.microsoft.portable-executable", "Signature");
            }

            // ISO base media: check the box and brand, not a hard-coded box size.
            if (content.Length >= 16 && HasAscii(content, 4, "ftyp"))
            {
                uint size = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(content.AsSpan(0, 4));
                if (size >= 16 && size <= content.Length && (size - 16) % 4 == 0 &&
                    new[] { "isom", "iso2", "mp41", "mp42", "avc1", "M4V " }.Any(b => HasAscii(content, 8, b)))
                    return Found(fileName, ".mp4", "video/mp4", "Container");
            }

            foreach (var type in KnownTypes)
                if (type.Matches(content)) return Found(fileName, type.Extension,
                    MimeTypeMap.Mappings.TryGetValue(type.Extension, out string? mime) ? mime : "application/octet-stream", "Signature");

            return Failure("UNSUPPORTED_FORMAT", "The binary format is not recognized.");
        }

        internal static DetectionResult Failure(string code, string message, string method = "Unknown", string mime = "application/octet-stream") => new()
        {
            MimeType = mime, DetectedExtension = "", DetectionMethod = method,
            ErrorCode = code, ErrorMessage = message
        };

        internal static DetectionResult Found(string? fileName, string extension, string mime, string method) => new()
        {
            MimeType = mime, DetectedExtension = extension, DetectionMethod = method,
            IsRecognized = true, ExtensionMismatch = !ExtensionMatches(fileName, extension),
            ErrorCode = "", ErrorMessage = ""
        };

        internal static bool ExtensionMatches(string? fileName, string detected)
        {
            // Treat both slash styles as separators, irrespective of the host OS.
            string name = (fileName ?? "").Replace('\\', '/').Split('/').Last();
            int dot = name.LastIndexOf('.');
            string ext = dot >= 0 ? name[dot..].ToLowerInvariant() : "";
            if (ext == detected) return true;
            return (detected, ext) is (".jpg", ".jpeg" or ".jpe") or (".tif", ".tiff") or (".exe", ".dll");
        }

        private static bool HasAscii(byte[] content, int offset, string text) => Has(content, offset, Encoding.ASCII.GetBytes(text));
        private static bool Has(byte[] content, int offset, byte[] signature) =>
            offset >= 0 && offset <= content.Length - signature.Length && content.AsSpan(offset, signature.Length).SequenceEqual(signature);

        private static bool HasTwoMp3Frames(byte[] bytes)
        {
            int start = 0;
            if (HasAscii(bytes, 0, "ID3"))
            {
                if (bytes.Length < 10 || bytes[3] is < 2 or > 4 || (bytes[5] & 0x0F) != 0 ||
                    bytes.AsSpan(6, 4).ContainsAnyInRange((byte)128, byte.MaxValue)) return false;
                start = 10 + (bytes[6] << 21) + (bytes[7] << 14) + (bytes[8] << 7) + bytes[9];
                if (bytes[3] == 4 && (bytes[5] & 0x10) != 0) start += 10;
            }
            int length = Mp3FrameLength(bytes, start);
            return length > 0 && Mp3FrameLength(bytes, start + length) > 0 &&
                (bytes[start + 1] & 0x1E) == (bytes[start + length + 1] & 0x1E) &&
                (bytes[start + 2] & 0x0C) == (bytes[start + length + 2] & 0x0C);
        }

        private static int Mp3FrameLength(byte[] bytes, int offset)
        {
            if (offset < 0 || offset > bytes.Length - 4 || bytes[offset] != 255 ||
                (bytes[offset + 1] & 0xE6) != 0xE2) return 0; // sync and Layer III
            int version = (bytes[offset + 1] >> 3) & 3;
            int bitrateIndex = bytes[offset + 2] >> 4;
            int rateIndex = (bytes[offset + 2] >> 2) & 3;
            if (version == 1 || bitrateIndex is 0 or 15 || rateIndex == 3 || (bytes[offset + 3] & 3) == 2) return 0;
            int[] bitrates = version == 3
                ? new[] { 0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320 }
                : new[] { 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160 };
            int rate = new[] { 44100, 48000, 32000 }[rateIndex] / (version == 3 ? 1 : version == 2 ? 2 : 4);
            int length = (version == 3 ? 144000 : 72000) * bitrates[bitrateIndex] / rate + ((bytes[offset + 2] >> 1) & 1);
            return length <= bytes.Length - offset ? length : 0;
        }
    }
}
