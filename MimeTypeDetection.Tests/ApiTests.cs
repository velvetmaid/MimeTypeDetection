using System.Reflection;
using System.Text;
using OutSystems.ExternalLibraries.SDK;
using Xunit;

namespace MimeTypeDetection.Tests;

public class ApiTests
{
    private readonly IMimeTypeDetection api = new MimeTypeDetectionService();
    private static readonly byte[] Jpeg = Convert.FromHexString("FFD8FF");

    [Fact]
    public void LegacyActionRetainsShapeAndUsesBinary()
    {
        var method = typeof(IMimeTypeDetection).GetMethod("GetFileMimeType")!;
        Assert.Equal(typeof(FileTypeRecord), method.ReturnType);
        Assert.Equal(new[] { "FileName", "FileContent" }, method.GetParameters().Select(p => p.Name));
        Assert.Equal(new[] { typeof(string), typeof(byte[]) }, method.GetParameters().Select(p => p.ParameterType));
        Assert.Equal(new[] { "Extension", "MimeType", "ErrorMessage" }, typeof(FileTypeRecord).GetFields().Select(f => f.Name));
        var result = api.GetFileMimeType("a.exe", Jpeg);
        Assert.Equal(".jpg", result.Extension);
        Assert.Equal("image/jpeg", result.MimeType);
        Assert.Equal("", result.ErrorMessage);
        Assert.Equal("application/octet-stream", api.GetFileMimeType("a.pdf", Array.Empty<byte>()).MimeType);
        Assert.Equal(".docx", api.GetFileMimeType("a.zip", DetectionTests.Office()).Extension);
    }

    [Fact]
    public void DefaultValidationAcceptsRecognizedContent()
    {
        var result = api.ValidateFile("renamed.exe", Jpeg, default);
        Assert.True(result.IsValid);
        Assert.Equal("", result.ErrorCode);
        Assert.True(result.Detection.ExtensionMismatch);
    }

    [Theory]
    [InlineData(" image/jpeg ; application/pdf ", true)]
    [InlineData("IMAGE/JPEG,application/pdf", true)]
    [InlineData("application/pdf", false)]
    public void AllowlistUsesExactCaseInsensitiveMimeTypes(string allowlist, bool valid)
    {
        var result = api.ValidateFile("a.jpg", Jpeg, new ValidationOptions { AllowedMimeTypes = allowlist });
        Assert.Equal(valid, result.IsValid);
        Assert.Equal(valid ? "" : "MIME_NOT_ALLOWED", result.ErrorCode);
    }

    [Theory]
    [InlineData("image/*")]
    [InlineData("image/jpeg; charset=utf-8")]
    [InlineData("image")]
    [InlineData(",;")]
    public void InvalidAllowlistFailsClosed(string allowlist)
    {
        var result = api.ValidateFile("a.jpg", Jpeg, new ValidationOptions { AllowedMimeTypes = allowlist });
        Assert.False(result.IsValid);
        Assert.Equal("INVALID_OPTIONS", result.ErrorCode);
        Assert.Equal("NOT_INSPECTED", result.Detection.ErrorCode);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(52428801)]
    public void InvalidSizeFailsClosed(long max) =>
        Assert.Equal("INVALID_OPTIONS", api.ValidateFile("a.jpg", Jpeg, new ValidationOptions { MaxFileSizeBytes = max }).ErrorCode);

    [Fact]
    public void LargeAllowlistIsRejected() =>
        Assert.Equal("INVALID_OPTIONS", api.ValidateFile("a.jpg", Jpeg, new ValidationOptions { AllowedMimeTypes = new string('x', 4097) }).ErrorCode);

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    public void ConfiguredSizeIsInclusive(int max, bool valid)
    {
        var result = api.ValidateFile("a.jpg", Jpeg, new ValidationOptions { MaxFileSizeBytes = max });
        Assert.Equal(valid, result.IsValid);
        Assert.Equal(valid ? "" : "FILE_TOO_LARGE", result.ErrorCode);
    }

    [Theory]
    [InlineData("a.exe", false)]
    [InlineData("no-extension", false)]
    [InlineData("a.JPG", true)]
    [InlineData("a.jpeg", true)]
    public void MatchingExtensionIsOptionalPolicy(string name, bool valid)
    {
        var result = api.ValidateFile(name, Jpeg, new ValidationOptions { RequireMatchingExtension = true });
        Assert.Equal(valid, result.IsValid);
        Assert.Equal(valid ? "" : "EXTENSION_MISMATCH", result.ErrorCode);
        Assert.True(result.Detection.IsRecognized);
        Assert.Equal("", result.Detection.ErrorCode);
    }

    [Fact]
    public void UnsupportedContentCannotPassUsingNameOrAllowlist()
    {
        var result = api.ValidateFile("a.pdf", Encoding.UTF8.GetBytes("hello"), new ValidationOptions { AllowedMimeTypes = "application/pdf" });
        Assert.False(result.IsValid);
        Assert.Equal("UNSUPPORTED_FORMAT", result.ErrorCode);
    }

    [Fact]
    public void EmptyContentCannotPass() => Assert.False(api.ValidateFile("a.pdf", null!, default).IsValid);

    [Fact]
    public void AmbiguousOleCannotPass() =>
        Assert.False(api.ValidateFile("a.doc", Convert.FromHexString("D0CF11E0A1B11AE1"), default).IsValid);

    [Fact]
    public void MalformedArchiveCannotPass() =>
        Assert.Equal("INVALID_ARCHIVE", api.ValidateFile("a.zip", Convert.FromHexString("504B0304"), default).ErrorCode);

    [Fact]
    public void ResourceLimitedArchiveCannotPass() =>
        Assert.Equal("ARCHIVE_LIMIT_EXCEEDED", api.ValidateFile("a.zip", DetectionTests.Zip(("[Content_Types].xml", new string('x', 65537))), default).ErrorCode);

    // This checks SDK metadata shape locally, NOT ODC importer/runtime compatibility.
    [Fact]
    public void OdcSurfaceUsesAttributedStructuresAndSimpleTypes()
    {
        Assert.NotNull(typeof(IMimeTypeDetection).GetCustomAttribute<OSInterfaceAttribute>());
        var methods = typeof(IMimeTypeDetection).GetMethods();
        Assert.Equal(new[] { "DetectMimeType", "GetFileMimeType", "ValidateFile" }, methods.Select(m => m.Name).Order());
        foreach (var method in methods)
        {
            Assert.NotNull(method.GetCustomAttribute<OSActionAttribute>());
            CheckType(method.ReturnType);
            foreach (var parameter in method.GetParameters()) CheckType(parameter.ParameterType);
        }
        Assert.NotNull(typeof(MimeTypeDetectionService).GetConstructor(Type.EmptyTypes));
        Assert.Contains("MimeTypeDetection.Resources.logo.png", typeof(IMimeTypeDetection).Assembly.GetManifestResourceNames());
        Assert.DoesNotContain(typeof(IMimeTypeDetection).Assembly.GetTypes(), t => t.Name == "Program" || t.Namespace == "MimeTypeDetection.Tests");
    }

    private static void CheckType(Type type)
    {
        if (new[] { typeof(string), typeof(bool), typeof(long), typeof(byte[]) }.Contains(type)) return;
        Assert.True(type.IsValueType);
        Assert.NotNull(type.GetCustomAttribute<OSStructureAttribute>());
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance)) CheckType(field.FieldType);
    }
}
