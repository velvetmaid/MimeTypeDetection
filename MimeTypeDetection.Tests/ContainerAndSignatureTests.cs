using System.Buffers.Binary;
using System.Text;
using Xunit;

namespace MimeTypeDetection.Tests;

public class ContainerAndSignatureTests
{
    private readonly MimeTypeService service = new();

    [Theory]
    [InlineData("424D", ".bmp")]
    [InlineData("00000100", ".ico")]
    [InlineData("0001000000", ".ttf")]
    [InlineData("526172211A0700", ".rar")]
    [InlineData("526172211A070100", ".rar")]
    [InlineData("377ABCAF271C", ".7z")]
    [InlineData("1F9D", ".z")]
    [InlineData("425A68", ".bz2")]
    [InlineData("1F8B08", ".gz")]
    [InlineData("78617221", ".xar")]
    [InlineData("4D534346", ".cab")]
    [InlineData("49545346", ".chm")]
    [InlineData("EDABEEDB", ".rpm")]
    [InlineData("465753", ".swf")]
    [InlineData("435753", ".swf")]
    [InlineData("60EA", ".arj")]
    [InlineData("00002D6C68352D", ".lzh")]
    public void RetainedSignaturesHaveMimeMappings(string hex, string extension)
    {
        var result = service.Detect("file" + extension, Convert.FromHexString(hex));
        Assert.True(result.IsRecognized);
        Assert.Equal(extension, result.DetectedExtension);
        Assert.NotEqual("application/octet-stream", result.MimeType);
    }

    [Fact]
    public void IsoUsesPrimaryVolumeDescriptorOffset()
    {
        byte[] bytes = new byte[32775];
        Convert.FromHexString("01434430303101").CopyTo(bytes, 32768);
        Assert.Equal(".iso", service.Detect("file.bin", bytes).DetectedExtension);
    }

    [Theory]
    [InlineData("koly", ".dmg")]
    [InlineData("conectix", ".vhd")]
    public void DiskImagesUseFooterRatherThanFirstByte(string signature, string extension)
    {
        byte[] bytes = new byte[1024];
        Encoding.ASCII.GetBytes(signature).CopyTo(bytes, 512);
        Assert.Equal(extension, service.Detect("file.bin", bytes).DetectedExtension);
        Assert.False(service.Detect("file" + extension, Encoding.ASCII.GetBytes(signature)).IsRecognized);
    }

    [Fact]
    public void PortableExecutableRequiresPeHeader()
    {
        byte[] bytes = new byte[100];
        Encoding.ASCII.GetBytes("MZ").CopyTo(bytes, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(60, 4), 80);
        Convert.FromHexString("50450000").CopyTo(bytes, 80);
        Assert.False(service.Detect("file.DLL", bytes).ExtensionMismatch);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(60, 4), uint.MaxValue);
        Assert.False(service.Detect("file.exe", bytes).IsRecognized);
    }

    [Fact]
    public void OggDoesNotGuessCodec()
    {
        byte[] bytes = new byte[27];
        Encoding.ASCII.GetBytes("OggS").CopyTo(bytes, 0);
        Assert.Equal("application/ogg", service.Detect("file.ogg", bytes).MimeType);
    }

    [Fact]
    public void AsfDoesNotGuessAudioOrVideo() =>
        Assert.Equal(".asf", service.Detect("file.wma", Convert.FromHexString("3026B2758E66CF11A6D900AA0062CE6C")).DetectedExtension);

    [Fact]
    public void EbmlDoesNotDefaultToMatroska() =>
        Assert.Equal("AMBIGUOUS_FORMAT", service.Detect("file.mkv", Convert.FromHexString("1A45DFA3")).ErrorCode);

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public void Mp3RequiresTwoCompleteFrames(int prefix)
    {
        byte[] bytes = new byte[prefix + 834]; // MPEG1 Layer III, 128kbit/s, 44100Hz: 417 bytes/frame
        if (prefix > 0) Convert.FromHexString("49443304000000000000").CopyTo(bytes, 0);
        Convert.FromHexString("FFFB9000").CopyTo(bytes, prefix);
        Assert.False(service.Detect("file.mp3", bytes).IsRecognized);
        Convert.FromHexString("FFFB9000").CopyTo(bytes, prefix + 417);
        Assert.Equal(".mp3", service.Detect("file.mp3", bytes).DetectedExtension);
        Assert.False(service.Detect("file.mp3", bytes[..^1]).IsRecognized);
    }

    [Fact]
    public void Id3SizeCannotCauseOverflowOrOutOfBounds()
    {
        Assert.False(service.Detect("a.mp3", Convert.FromHexString("4944330400007F7F7F7F")).IsRecognized);
        Assert.False(service.Detect("a.mp3", Convert.FromHexString("494433040000FFFFFFFF")).IsRecognized);
    }

    [Theory]
    [InlineData("isom", true)]
    [InlineData("mp42", true)]
    [InlineData("heic", false)]
    [InlineData("qt  ", false)]
    public void IsoBaseMediaRequiresSupportedMajorBrand(string brand, bool recognized)
    {
        byte[] bytes = new byte[24];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, 24);
        Encoding.ASCII.GetBytes("ftyp" + brand).CopyTo(bytes, 4);
        Assert.Equal(recognized, service.Detect("a.mp4", bytes).IsRecognized);
        BinaryPrimitives.WriteUInt32BigEndian(bytes, 1000);
        Assert.False(service.Detect("a.mp4", bytes).IsRecognized);
    }

    [Fact]
    public void BoundedAndFixedSignaturesNeverReadPastContent()
    {
        var fixedSignature = new FileType("test", ".test", new byte?[] { 1, null, 3 }, 4, 4);
        var searchSignature = new FileType("test", ".test", new byte?[] { 1, 2, 3 }, 2);
        Assert.True(fixedSignature.Matches(new byte[] { 0, 0, 0, 0, 1, 9, 3 }));
        Assert.False(fixedSignature.Matches(new byte[] { 0, 0, 0, 1, 9, 3 }));
        Assert.False(searchSignature.Matches(new byte[] { 1, 2 }));
        Assert.True(searchSignature.Matches(new byte[] { 0, 0, 1, 2, 3 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FileType("test", ".test", new byte?[] { 1 }, 5, 4));
    }

    [Fact]
    public void RandomMalformedInputsReturnStructuredResults()
    {
        var random = new Random(8128);
        for (int i = 0; i < 2000; i++)
        {
            byte[] bytes = new byte[random.Next(0, 1024)];
            random.NextBytes(bytes);
            if (i % 2 == 0 && bytes.Length >= 4) Convert.FromHexString("504B0304").CopyTo(bytes, 0);
            var result = service.Detect("file.docx", bytes);
            Assert.NotNull(result.MimeType);
            Assert.NotNull(result.ErrorCode);
            if (!result.IsRecognized) Assert.NotEmpty(result.ErrorCode);
        }
    }
}
