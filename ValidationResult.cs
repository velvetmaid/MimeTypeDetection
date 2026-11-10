using OutSystems.ExternalLibraries.SDK;

namespace MimeTypeDetection;

[OSStructure(Description = "Policy validation only; does not scan for malware or validate the complete file format.")]
public struct ValidationResult
{
    [OSStructureField(Description = "Binary detection evidence. NOT_INSPECTED if validation options are invalid.")]
    public DetectionResult Detection;
    [OSStructureField(Description = "True only when a recognized type passes the configured upload policy. Not a safety guarantee.")]
    public bool IsValid;
    [OSStructureField(Description = "Detection failure or policy rejection code; empty on success.")]
    public string ErrorCode;
    [OSStructureField(Description = "Safe diagnostic message for the detection or policy failure.")]
    public string ErrorMessage;
}
