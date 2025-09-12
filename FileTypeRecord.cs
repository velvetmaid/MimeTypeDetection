using OutSystems.ExternalLibraries.SDK;

namespace MimeTypeDetection
{
    [OSStructure]
    public struct FileTypeRecord
    {
        public string Extension;
        public string MimeType;
        public string ErrorMessage;

        public FileTypeRecord(string extension, string mimeType, string errorMessage = "")
        {
            Extension = extension;
            MimeType = mimeType;
            ErrorMessage = errorMessage;
        }
    }
}
