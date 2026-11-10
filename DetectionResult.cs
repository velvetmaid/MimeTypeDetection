using OutSystems.ExternalLibraries.SDK;

namespace MimeTypeDetection;

[OSStructure(Description = "Binary detection evidence, not a guarantee of file safety or integrity.")]
public struct DetectionResult
{
    [OSStructureField(Description = "Detected MIME type, or application/octet-stream when unknown.")]
    public string MimeType;
    [OSStructureField(Description = "Canonical extension including dot; empty when no specific supported type is identified.")]
    public string DetectedExtension;
    [OSStructureField(Description = "Signature, Container, or Unknown; never inferred from the filename.")]
    public string DetectionMethod;
    [OSStructureField(Description = "True only for a supported, identified binary type. Does not imply file integrity or safety.")]
    public bool IsRecognized;
    [OSStructureField(Description = "For recognized types, true when the filename extension is missing or disagrees with detection.")]
    public bool ExtensionMismatch;
    [OSStructureField(Description = "Stable machine-readable error code; empty on successful identification.")]
    public string ErrorCode;
    [OSStructureField(Description = "Safe diagnostic message, never raw exception details.")]
    public string ErrorMessage;
}
