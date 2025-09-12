using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace MimeTypeDetection
{
    public class MimeTypeService
    {
        private static readonly IReadOnlyDictionary<string, string> _map = MimeTypeMap.Mappings;

        // Signature-based detection
        private static readonly IList<FileType> knownFileTypes = new List<FileType>
        {
            // ===== Common file types =====
            new FileType("Bitmap", ".bmp", new byte?[] { 0x42, 0x4d }),
            new FileType("Portable Network Graphic", ".png", new byte?[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            new FileType("JPEG", ".jpg", new byte?[] { 0xFF, 0xD8, 0xFF }),
            new FileType("JPEG", ".jpeg", new byte?[] { 0xFF, 0xD8, 0xFF }),
            new FileType("GIF", ".gif", new byte?[] { 0x47, 0x49, 0x46, 0x38 }),
            new FileType("Graphics Interchange Format 87a", ".gif", new byte?[] { 0x47, 0x49, 0x46, 0x38, 0x37, 0x61 }),
            new FileType("Graphics Interchange Format 89a", ".gif", new byte?[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }),
            new FileType("ICO", ".ico", new byte?[] { 0, 0, 1, 0 }),
            new FileType("TIFF", ".tif", new byte?[] { 73, 73, 42, 0 }),
            new FileType("TIFF", ".tif", new byte?[] { 0x4D, 0x4D, 0x00, 0x2A }),
            new FileType("TTF", ".ttf", new byte?[] { 0, 1, 0, 0, 0 }),
            new FileType("ICO", ".ico", new byte?[] { 0, 0, 1, 0}),
            new FileType("EXE_DLL", ".exe", new byte?[] { 77, 90 }),
            new FileType("OGG", ".ogg", new byte?[] { 79, 103, 103, 83, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0 }),
            new FileType("SWF", ".swf", new byte?[] { 70, 87, 83 }),
            new FileType("WAV_AVI", ".avi", new byte?[] { 82, 73, 70, 70 }),
            new FileType("WMV_WMA", ".wma", new byte?[] { 48, 38, 178, 117, 142, 102, 207, 17, 166, 217, 0, 170, 0, 98, 206, 108 }),
            new FileType("MP3", ".mp3", new byte?[] { 255, 251, 48 }),
            new FileType("MKV", ".mkv", new byte?[] { 0x1A, 0x45, 0xDF, 0xA3 }),
            new FileType("TORRENT", ".torrent", new byte?[] { 100, 56, 58, 97, 110, 110, 111, 117, 110, 99, 101 }),
            new FileType("TXT", ".txt", new byte?[] {0x46, 0x4F, 0x52, 0x4D, null, null, null, null,0x46, 0x54, 0x58, 0x54 }),
            new FileType("Portable Document Format", ".pdf", new byte?[] { 0x25, 0x50, 0x44, 0x46 }, 1019),
            new FileType("Portable Document Format", ".pdf", new byte?[] { 37, 80, 68, 70, 45, 49, 46 }),
            new FileType("Microsoft Excel", ".xls", new byte?[] { 0x09, 0x08, 0x10, 0x00, 0x00, 0x06, 0x05, 0x00 }, 512),
          //new FileType("Microsoft Office", ".office", new byte?[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00 }),
          //new FileType("Microsoft Word", ".docx", new byte?[] { 80, 75, 3, 4, 20 }),
          /*new FileType("Microsoft Excel", ".xlsx222", new byte?[] { 0x50, 0x4B, 0x05, 0x06 }),
            new FileType("Microsoft Excel", ".xlsx444", new byte?[] { 0x50, 0x4B, 0x07, 0x08 }),
            new FileType("Microsoft Excel", ".xlsx3333", new byte?[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x06, 0x00 }),*/
            new FileType("Microsoft Office Word - Legacy", ".doc", new byte?[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }),
            new FileType("Microsoft Office Excel - Legacy", ".xls", new byte?[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }),
            new FileType("Microsoft Office PowerPoint - Legacy", ".ppt", new byte?[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }),
            new FileType("Outlook Message File", ".msg", new byte?[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }),
          /*new FileType("Microsoft Office Word", ".docx", new byte?[] { 0x50, 0x4B, 0x03, 0x04 }),
            new FileType("Microsoft Office Excel", ".xlsx", new byte?[] { 0x50, 0x4B, 0x03, 0x04 }),
            new FileType("Microsoft Office PowerPoint", ".pptx", new byte?[] { 0x50, 0x4B, 0x03, 0x04 }),*/
            new FileType("Microsoft Office Word", ".doc", new byte?[] { 0xEC, 0xA5, 0xC1, 0x00 }, 512),
          //new FileType("Microsoft Office Word", ".doc", new byte?[] { 208, 207, 17, 224, 161, 177, 26, 225 }),
            new FileType("Microsoft Office PowerPoint", ".ppt", new byte?[] { 0xFD, 0xFF, 0xFF, 0xFF, null, 0x00, 0x00, 0x00 }, 512),
            new FileType("RAR", ".rar", new byte?[] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x00}),
            new FileType("RAR", ".rar", new byte?[] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x01, 0x00}),
            new FileType("TAR archive", ".tar", new byte?[] { 0x75, 0x73, 0x74, 0x61, 0x72 }, 257),
            new FileType("TAR ZIP", ".z", new byte?[] { 0x1F, 0x9D }),
            new FileType("TAR ZIP", ".z", new byte?[] { 0x1F, 0x9D }),
            new FileType("Bzip2", ".bz2", new byte?[] { 0x42, 0x5A, 0x68 }),
            new FileType("GZIP", ".gz", new byte?[] { 0x1F, 0x8B }),
            new FileType("eXtensible ARchive format", ".xar", new byte?[] { 0x78, 0x61, 0x72, 0x21 }),
            new FileType("ISO9660 CD/DVD image file", ".iso", new byte?[] { 0x43, 0x44, 0x30, 0x30, 0x31 }),
            new FileType("Apple Disk Image file", ".dmg", new byte?[] { 0x78, 0x01, 0x73, 0x0D, 0x62, 0x62, 0x60 }),
            new FileType("Microsoft Cabinet file", ".cab", new byte?[] { 0x4D, 0x53, 0x43, 0x46 }),
            new FileType("Install Shield compressed file", ".cab", new byte?[] { 0x49, 0x53, 0x63, 0x28 }),
            new FileType("ARJ", ".arj", new byte?[] { 0x60, 0xEA }),
            new FileType("MS Compiled HTML Help File", ".cab", new byte?[] { 0x49, 0x54, 0x53, 0x46 }),
            new FileType("MacOS X image file", ".dmg", new byte?[] { 0x78 }),
            new FileType("LZH", ".lzh", new byte?[] { 0x2D, 0x6C, 0x68 }),
            new FileType("RedHat Package Manager", ".rpm", new byte?[] { 0xED, 0xAB, 0xEE, 0xDB }),
            new FileType("Shockwave Flash file", ".swf", new byte?[] { 0x43, 0x57, 0x53 }),
            new FileType("Virtual PC HD image", ".vhd", new byte?[] { 0x63, 0x6F, 0x6E, 0x65, 0x63, 0x74, 0x69, 0x78 }),
            new FileType("ZIP", ".zip", new byte?[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x01, 0x00 }),
            new FileType("ZIP", ".zip", new byte?[] { 0x57, 0x69, 0x6E, 0x5A, 0x69, 0x70 }),
            new FileType("ZIP", ".zip", new byte?[] { 0x50, 0x4B, 0x07, 0x08 }),
            new FileType("ZIP", ".zip", new byte?[] { 0x50, 0x4B, 0x05, 0x06 }),
            new FileType("ZIP", ".zip", new byte?[] { 0x50, 0x4B, 0x53, 0x70, 0x58 }),
            new FileType("ZIP", ".zip", new byte?[] { 0x50, 0x4B, 0x4C, 0x49, 0x54, 0x45 }),
            new FileType("ZIP", ".zip", new byte?[] { 0x50, 0x4B, 0x03, 0x04 }),
            new FileType("7-Zip", ".7z", new byte?[] { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C }),
            new FileType("GemBox.Spreadsheet", ".xlsx", new byte?[] { 0x50, 0x4B, 0x07, 0x08 }),
            new FileType("MP4", ".mp4", new byte?[] { 0x00, 0x00, 0x00, 0x14, 0x66, 0x74, 0x79, 0x70, 0x69, 0x73, 0x6F, 0x6D }),
            new FileType("MP4", ".mp4", new byte?[] { 0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70, 0x33, 0x67, 0x70, 0x35 }),
            new FileType("MP4", ".mp4", new byte?[] { 0x00, 0x00, 0x00, 0x14, 0x66, 0x74, 0x79, 0x70, 0x6D, 0x70, 0x34, 0x32 }),
            new FileType("MP4", ".mp4", new byte?[] { 0x00, 0x00, 0x00, 0x1C, 0x66, 0x74, 0x79, 0x70, 0x4D, 0x53, 0x4E, 0x56, 0x01, 0x29, 0x00, 0x46, 0x4D, 0x53, 0x4E, 0x56, 0x6D, 0x70, 0x34, 0x32}),
            new FileType("MP4", ".mp4", new byte?[] { 0x66, 0x74, 0x79, 0x70, 0x33, 0x67, 0x70, 0x35 },4),
            new FileType("MP4", ".mp4", new byte?[] { 0x66, 0x74, 0x79, 0x70, 0x4D, 0x53, 0x4E, 0x56 },4),
            new FileType("MP4", ".mp4", new byte?[] { 0x66, 0x74, 0x79, 0x70, 0x69, 0x73, 0x6F, 0x6D },4),
            new FileType("MP4", ".mp4", new byte?[] { 0x66, 0x74, 0x79, 0x70, 0x6D, 0x70, 0x34, 0x32 },4),
        };

        private static readonly IList<FileType> UnknownFileTypes = new List<FileType>
        {
            new FileType("Excel Microsoft Office Open XML Format Spreadsheet file", ".xlsx", new byte?[] { 0x50, 0x4B, 0x03, 0x04 }),
            new FileType("Microsoft Word Open XML Document file", ".docx", new byte?[] { 0x50, 0x4B, 0x03, 0x04 }),
            new FileType("Microsoft PowerPoint Open XML Presentation file", ".pptx", new byte?[] { 0x50, 0x4B, 0x03, 0x04 }),
            new FileType("Excel Microsoft Office Open XML Format Spreadsheet file", ".xlsx", new byte?[] { 0x50, 0x4B, 0x05, 0x06 }),
            new FileType("Microsoft Word  Open XML Document file", ".docx", new byte?[] { 0x50, 0x4B, 0x05, 0x06 }),
            new FileType("Microsoft PowerPoint Open XML Presentation file", ".pptx", new byte?[] { 0x50, 0x4B, 0x05, 0x06 }),
            new FileType("Excel Microsoft Office Open XML Format Spreadsheet file", ".xlsx", new byte?[] { 0x50, 0x4B, 0x07, 0x08 }),
            new FileType("Microsoft Word  Open XML Document file", ".docx", new byte?[] { 0x50, 0x4B, 0x07, 0x08 }),
            new FileType("Microsoft PowerPoint Open XML Presentation file", ".pptx", new byte?[] { 0x50, 0x4B, 0x07, 0x08 }),
        }
        .OrderBy(f => f.MaximumStartLocation)
        .ThenBy(f => f.MagicSequence.Length)
        .ToList();

        private bool StartOfFileContainsFileType(FileType type, byte[] startOfFile)
        {
            var sig = type.MagicSequence;
            if (startOfFile.Length < sig.Length) return false;
            for (int i = 0; i < sig.Length; i++)
            {
                if (sig[i] is byte value && startOfFile[i] != value) return false;
            }
            return true;
        }

        // Main detection method 
        public FileTypeRecord GetFileType(string fileName, byte[]? content = null)
        {
            var ext = Path.GetExtension(fileName)?.Trim().ToLowerInvariant();

            try
            {
                // 1. Office Open XML special case
                if (ext == ".docx" || ext == ".xlsx" || ext == ".pptx")
                {
                    try
                    {
                        var (mime, error) = Helper.OfficeFileDetector.DetectOfficeFile(fileName);
                        return new FileTypeRecord(Path.GetExtension(fileName), mime, error);
                    }
                    catch (Exception ex)
                    {
                        return new FileTypeRecord(".zip", "application/zip", $"Unknown Office structure: {ex.Message}");
                    }
                }

                // 2. Signature match known file types
                if (content != null)
                {
                    foreach (var type in knownFileTypes)
                    {
                        if (StartOfFileContainsFileType(type, content))
                        {
                            return new FileTypeRecord(type.Extension,
                                _map.TryGetValue(type.Extension, out var mime) ? mime : "application/octet-stream");
                        }
                    }

                    // 3. Signature match unknown legacy types
                    foreach (var type in UnknownFileTypes)
                    {
                        if (StartOfFileContainsFileType(type, content))
                        {
                            return new FileTypeRecord(type.Extension,
                                _map.TryGetValue(type.Extension, out var mime) ? mime : "application/octet-stream");
                        }
                    }
                }

                // 4. Fallback by extension
                if (!string.IsNullOrEmpty(ext) && _map.TryGetValue(ext, out var fallbackMime))
                {
                    return new FileTypeRecord(ext, fallbackMime);
                }

                // 5. Fallback unknown
                return new FileTypeRecord(ext ?? string.Empty, "application/octet-stream");
            }
            catch (Exception ex)
            {
                // error fatal
                return new FileTypeRecord(ext ?? string.Empty, "application/octet-stream", ex.Message);
            }
        }
    }
}   