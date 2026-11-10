namespace MimeTypeDetection
{
    public class MimeTypeDetectionService : IMimeTypeDetection
    {
        private static readonly MimeTypeService Service = new();

        public FileTypeRecord GetFileMimeType(string fileName, byte[] fileContent)
        {
            return Service.GetFileType(fileName, fileContent);
        }

        public DetectionResult DetectMimeType(string fileName, byte[] fileContent) => Service.Detect(fileName, fileContent);

        public ValidationResult ValidateFile(string fileName, byte[] fileContent, ValidationOptions options) =>
            FileValidator.Validate(Service, fileName, fileContent, options);
    }
}
