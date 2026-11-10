using OutSystems.ExternalLibraries.SDK;

namespace MimeTypeDetection;

[OSStructure(Description = "Upload policy. A zero size uses the library limit; an empty allowlist permits any recognized type.")]
public struct ValidationOptions
{
    [OSStructureField(Description = "Maximum bytes, 0 for the 50 MiB library limit. Must not exceed 52428800.")]
    public long MaxFileSizeBytes;
    [OSStructureField(Description = "Comma- or semicolon-separated exact MIME types. Empty permits any recognized type; no wildcards.")]
    public string AllowedMimeTypes;
    [OSStructureField(Description = "Reject mismatched or missing filename extensions. Common JPEG/TIFF aliases and PE DLLs are accepted.")]
    public bool RequireMatchingExtension;
}
