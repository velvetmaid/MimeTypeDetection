using System.Buffers.Binary;
using Xunit;

namespace MimeTypeDetection.Tests;

public class ArchiveTests
{
    private readonly MimeTypeService service = new();
    private const string TypesNamespace = "http://schemas.openxmlformats.org/package/2006/content-types";
    private const string RelsNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";
    private const string OfficeRelationship = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument";
    private const string WordType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml";
    private const string ExcelType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml";

    [Fact]
    public void ManifestWithoutPackageRelationshipIsGenericZip()
    {
        byte[] bytes = DetectionTests.Zip(
            ("[Content_Types].xml", $"<Types xmlns='{TypesNamespace}'><Override PartName='/word/document.xml' ContentType='{WordType}'/></Types>"),
            ("word/document.xml", "<document/>"));
        Assert.Equal(".zip", service.Detect("fake.docx", bytes).DetectedExtension);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOrEmptyMainPartIsNotOffice(bool empty)
    {
        var entries = new List<(string, string)>
        {
            ("[Content_Types].xml", $"<Types xmlns='{TypesNamespace}'><Override PartName='/word/document.xml' ContentType='{WordType}'/></Types>"),
            ("_rels/.rels", $"<Relationships xmlns='{RelsNamespace}'><Relationship Type='{OfficeRelationship}' Target='word/document.xml'/></Relationships>")
        };
        if (empty) entries.Add(("word/document.xml", ""));
        Assert.Equal(".zip", service.Detect("fake.docx", DetectionTests.Zip(entries.ToArray())).DetectedExtension);
    }

    [Fact]
    public void MultipleOfficeSubtypesAreAmbiguous()
    {
        byte[] bytes = DetectionTests.Zip(
            ("[Content_Types].xml", $"<Types xmlns='{TypesNamespace}'><Override PartName='/word/document.xml' ContentType='{WordType}'/><Override PartName='/xl/workbook.xml' ContentType='{ExcelType}'/></Types>"),
            ("_rels/.rels", $"<Relationships xmlns='{RelsNamespace}'><Relationship Type='{OfficeRelationship}' Target='word/document.xml'/><Relationship Type='{OfficeRelationship}' Target='xl/workbook.xml'/></Relationships>"),
            ("word/document.xml", "<document/>"), ("xl/workbook.xml", "<workbook/>"));
        var result = service.Detect("ambiguous.docx", bytes);
        Assert.Equal("AMBIGUOUS_FORMAT", result.ErrorCode);
        Assert.False(result.IsRecognized);
    }

    [Fact]
    public void MacroPayloadDoesNotBecomeMacroFreeDocx()
    {
        byte[] bytes = DetectionTests.Zip(
            ("[Content_Types].xml", $"<Types xmlns='{TypesNamespace}'><Override PartName='/word/document.xml' ContentType='{WordType}'/></Types>"),
            ("_rels/.rels", $"<Relationships xmlns='{RelsNamespace}'><Relationship Type='{OfficeRelationship}' Target='word/document.xml'/></Relationships>"),
            ("word/document.xml", "<document/>"), ("word/vbaProject.bin", "macro bytes"));
        Assert.Equal(".zip", service.Detect("macros.docx", bytes).DetectedExtension);
    }

    [Fact]
    public void ExternalRelationshipsAreNotResolvedOrUsed()
    {
        byte[] bytes = DetectionTests.Zip(
            ("[Content_Types].xml", $"<Types xmlns='{TypesNamespace}'><Override PartName='/word/document.xml' ContentType='{WordType}'/></Types>"),
            ("_rels/.rels", $"<Relationships xmlns='{RelsNamespace}'><Relationship Type='{OfficeRelationship}' Target='word/document.xml' TargetMode='External'/></Relationships>"),
            ("word/document.xml", "<document/>"));
        Assert.Equal(".zip", service.Detect("external.docx", bytes).DetectedExtension);
    }

    [Fact]
    public void MalformedXmlHasSafeError() =>
        Assert.Equal("INVALID_ARCHIVE", service.Detect("a.docx", DetectionTests.Zip(("[Content_Types].xml", "<broken"))).ErrorCode);

    [Fact]
    public void MetadataBytesAreBoundedBeforeEntryAllocation()
    {
        var files = Enumerable.Range(0, 1600).Select(i => (new string('x', 700) + i, "")).ToArray();
        Assert.Equal("ARCHIVE_LIMIT_EXCEEDED", service.Detect("a.zip", DetectionTests.Zip(files)).ErrorCode);
    }

    [Fact]
    public void MaximumEntryCountIsInclusive()
    {
        var files = Enumerable.Range(0, 2048).Select(i => (i.ToString(), "")).ToArray();
        Assert.Equal(".zip", service.Detect("a.zip", DetectionTests.Zip(files)).DetectedExtension);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    public void MultiVolumeZipIsRejected(int diskOffset)
    {
        byte[] bytes = DetectionTests.Zip(("file", "data"));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(bytes.Length - 22 + diskOffset, 2), 1);
        Assert.Equal("INVALID_ARCHIVE", service.Detect("a.zip", bytes).ErrorCode);
    }

    [Fact]
    public void Zip64MarkersAreRejectedBeforeAllocation()
    {
        byte[] bytes = DetectionTests.Zip(("file", "data"));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(bytes.Length - 6, 4), uint.MaxValue);
        Assert.Equal("INVALID_ARCHIVE", service.Detect("a.zip", bytes).ErrorCode);
    }

    [Fact]
    public void InvalidLocalHeaderIsRejected()
    {
        byte[] bytes = DetectionTests.Zip(("file", "data"));
        int directory = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(bytes.Length - 6, 4));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(directory + 42, 4), uint.MaxValue);
        Assert.Equal("INVALID_ARCHIVE", service.Detect("a.zip", bytes).ErrorCode);
    }

    [Fact]
    public void ForgedEntryCountCannotHideEntries()
    {
        byte[] bytes = DetectionTests.Zip(("file", "data"));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(bytes.Length - 14, 2), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(bytes.Length - 12, 2), 0);
        Assert.Equal("INVALID_ARCHIVE", service.Detect("a.zip", bytes).ErrorCode);
    }

    [Fact]
    public void ZipDataDescriptorIsSupported()
    {
        byte[] original = DetectionTests.Zip(("file", "data"));
        int directory = (int)BinaryPrimitives.ReadUInt32LittleEndian(original.AsSpan(original.Length - 6, 4));
        byte[] bytes = new byte[original.Length + 16];
        original.AsSpan(0, directory).CopyTo(bytes);
        original.AsSpan(directory).CopyTo(bytes.AsSpan(directory + 16));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(directory, 4), 0x08074b50);
        original.AsSpan(14, 12).CopyTo(bytes.AsSpan(directory + 4));
        bytes[6] |= 8;
        bytes[directory + 16 + 8] |= 8;
        bytes.AsSpan(14, 12).Clear();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(bytes.Length - 6, 4), (uint)(directory + 16));
        Assert.Equal(".zip", service.Detect("a.zip", bytes).DetectedExtension);
    }

    [Fact]
    public void EncryptedZipIsNotAcceptedAsInspected()
    {
        byte[] bytes = DetectionTests.Zip(("file", "data"));
        int directory = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(bytes.Length - 6, 4));
        bytes[directory + 8] |= 1;
        Assert.Equal("INVALID_ARCHIVE", service.Detect("a.zip", bytes).ErrorCode);
    }

    [Theory]
    [InlineData(6)] // flags
    [InlineData(8)] // compression method
    [InlineData(14)] // CRC metadata
    [InlineData(18)] // compressed length
    [InlineData(22)] // expanded length
    [InlineData(26)] // filename length
    [InlineData(30)] // filename bytes
    public void InconsistentLocalAndCentralMetadataIsRejected(int localOffset)
    {
        byte[] bytes = DetectionTests.Zip(("file", "data"));
        bytes[localOffset] ^= 1;
        Assert.Equal("INVALID_ARCHIVE", service.Detect("a.zip", bytes).ErrorCode);
    }

    [Fact]
    public void ManifestDecompressionDoesNotTrustDeclaredSize()
    {
        byte[] bytes = DetectionTests.Zip(("[Content_Types].xml", new string('x', 65537)));
        int directory = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(bytes.Length - 6, 4));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(directory + 24, 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(22, 4), 1);
        var result = service.Detect("a.zip", bytes);
        Assert.False(result.IsRecognized);
        Assert.Contains(result.ErrorCode, new[] { "ARCHIVE_LIMIT_EXCEEDED", "INVALID_ARCHIVE" });
    }
}
