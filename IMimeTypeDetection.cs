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
    }
}
