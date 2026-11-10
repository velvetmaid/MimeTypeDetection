using OutSystems.ExternalLibraries.SDK;

namespace MimeTypeDetection
{
    [OSInterface(
        Description = "Mime Type Detection",
        IconResourceName = "MimeTypeDetection.Resources.logo.png",
        Name = "MimeTypeDetection"
    )]
    public interface IMimeTypeDetection
    {
        [OSAction(
            Description = "Get MIME type and extension from file binary",
            IconResourceName = "MimeTypeDetection.Resources.logo.png")]
        FileTypeRecord GetFileMimeType(string FileName, byte[] FileContent);

        [OSAction(Description = "Detect type from binary content. FileName is metadata only. Not a malware or integrity check.")]
        DetectionResult DetectMimeType(
            [OSParameter(Description = "Original upload filename, used only to compare extensions.")] string FileName,
            [OSParameter(DataType = OSDataType.BinaryData, Description = "Complete uploaded file bytes, at most 50 MiB.")] byte[] FileContent);

        [OSAction(Description = "Apply upload policy to binary detection. Not a malware or complete format integrity check.")]
        ValidationResult ValidateFile(
            [OSParameter(Description = "Original upload filename, never a server-side path.")] string FileName,
            [OSParameter(DataType = OSDataType.BinaryData, Description = "Complete uploaded file bytes.")] byte[] FileContent,
            [OSParameter(Description = "Size, MIME allowlist, and filename-extension policy.")] ValidationOptions Options);
    }
}
