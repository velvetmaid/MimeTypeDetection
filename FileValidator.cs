namespace MimeTypeDetection;

internal static class FileValidator
{
    internal static ValidationResult Validate(MimeTypeService service, string? fileName, byte[]? content, ValidationOptions options)
    {
        var result = new ValidationResult
        {
            Detection = MimeTypeService.Failure("NOT_INSPECTED", "Content was not inspected."),
            ErrorCode = "", ErrorMessage = ""
        };
        string allowlist = options.AllowedMimeTypes ?? "";
        if (options.MaxFileSizeBytes < 0 || options.MaxFileSizeBytes > MimeTypeService.MaxContentBytes || allowlist.Length > 4096)
            return Reject(result, "INVALID_OPTIONS", "Use a size from 0 to 52428800 bytes and an allowlist of at most 4096 characters.");

        string[] allowed = allowlist.Split(new[] { ',', ';' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if ((!string.IsNullOrWhiteSpace(allowlist) && allowed.Length == 0) || allowed.Any(m => !IsMimeType(m)))
            return Reject(result, "INVALID_OPTIONS", "AllowedMimeTypes must contain comma- or semicolon-separated exact MIME types, without wildcards or parameters.");

        long max = options.MaxFileSizeBytes == 0 ? MimeTypeService.MaxContentBytes : options.MaxFileSizeBytes;
        if (content != null && content.LongLength > max)
        {
            result.Detection = MimeTypeService.Failure("FILE_TOO_LARGE", "File exceeds the configured size limit.");
            return Reject(result, result.Detection.ErrorCode, result.Detection.ErrorMessage);
        }

        result.Detection = service.Detect(fileName, content);
        if (!result.Detection.IsRecognized || result.Detection.ErrorCode != "")
            return Reject(result, result.Detection.ErrorCode, result.Detection.ErrorMessage);
        if (allowed.Length > 0 && !allowed.Contains(result.Detection.MimeType, StringComparer.OrdinalIgnoreCase))
            return Reject(result, "MIME_NOT_ALLOWED", "Detected MIME type is not allowed.");
        if (options.RequireMatchingExtension && result.Detection.ExtensionMismatch)
            return Reject(result, "EXTENSION_MISMATCH", "Filename extension does not match the detected type.");
        result.IsValid = true;
        return result;
    }

    private static ValidationResult Reject(ValidationResult result, string code, string message)
    {
        result.IsValid = false;
        result.ErrorCode = code;
        result.ErrorMessage = message;
        return result;
    }

    private static bool IsMimeType(string value)
    {
        string[] parts = value.Split('/');
        return parts.Length == 2 && parts.All(p => p.Length > 0 && p.All(c =>
            char.IsAsciiLetterOrDigit(c) || "!#$%&'+-.^_`|~".Contains(c)));
    }
}
