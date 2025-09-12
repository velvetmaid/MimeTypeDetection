using MimeTypeDetection;

namespace MimeTypeDetection
{
    public class MimeTypeDetectionService : IMimeTypeDetection
    {
        public FileTypeRecord GetFileMimeType(string fileName, byte[] fileContent)
        {
            var service = new MimeTypeService();
            var result = service.GetFileType(fileName, fileContent);

            return new FileTypeRecord(result.Extension, result.MimeType, result.ErrorMessage);
        }
    }
}
