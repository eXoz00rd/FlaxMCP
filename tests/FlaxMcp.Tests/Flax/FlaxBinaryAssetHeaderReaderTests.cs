using System.Text;
using FlaxMcp.Flax;
using Xunit;

namespace FlaxMcp.Tests.Flax;

public sealed class FlaxBinaryAssetHeaderReaderTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "FlaxMcpTests_" + Guid.NewGuid().ToString("N"));

    // Real GUID bytes from D:\Projects\Mournfall\Content\Materials\Floor Material.flax, cross-checked
    // against Content/Scenes/Main.scene's "Material": "c5bf75264ac08f205d16eea554e3d16e" reference.
    private static readonly byte[] RealFloorMaterialGuidBytes =
    [
        0x26, 0x75, 0xBF, 0xC5, 0x20, 0x8F, 0xC0, 0x4A, 0xA5, 0xEE, 0x16, 0x5D, 0x6E, 0xD1, 0xE3, 0x54,
    ];

    public FlaxBinaryAssetHeaderReaderTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void TryRead_ParsesRealFlaxMaterialHeaderBytes()
    {
        var filePath = Path.Combine(_tempDir, "Floor Material.flax");
        WriteHeader(filePath, "CFWF"u8.ToArray(), RealFloorMaterialGuidBytes, "FlaxEngine.Material");

        var result = FlaxBinaryAssetHeaderReader.TryRead(filePath);

        Assert.NotNull(result);
        Assert.Equal("c5bf75264ac08f205d16eea554e3d16e", result.Value.Id);
        Assert.Equal("FlaxEngine.Material", result.Value.TypeName);
    }

    [Fact]
    public void TryRead_WithWrongMagic_ReturnsNull()
    {
        var filePath = Path.Combine(_tempDir, "NotFlax.flax");
        WriteHeader(filePath, "XXXX"u8.ToArray(), RealFloorMaterialGuidBytes, "FlaxEngine.Material");

        var result = FlaxBinaryAssetHeaderReader.TryRead(filePath);

        Assert.Null(result);
    }

    [Fact]
    public void TryRead_WithEmptyTypeName_ReturnsNull()
    {
        var filePath = Path.Combine(_tempDir, "EmptyTypeName.flax");
        WriteHeader(filePath, "CFWF"u8.ToArray(), RealFloorMaterialGuidBytes, string.Empty);

        var result = FlaxBinaryAssetHeaderReader.TryRead(filePath);

        Assert.Null(result);
    }

    [Fact]
    public void TryRead_WithTruncatedFile_ReturnsNull()
    {
        var filePath = Path.Combine(_tempDir, "Truncated.flax");
        File.WriteAllBytes(filePath, "CFWF"u8.ToArray());

        var result = FlaxBinaryAssetHeaderReader.TryRead(filePath);

        Assert.Null(result);
    }

    private static void WriteHeader(string filePath, byte[] magic, byte[] guidBytes, string typeName)
    {
        using var stream = File.Create(filePath);
        using var writer = new BinaryWriter(stream);

        writer.Write(magic);
        writer.Write(9); // format version
        writer.Write(new byte[16]); // reserved
        writer.Write(1); // unknown flag/count
        writer.Write(guidBytes);
        writer.Write(Encoding.Unicode.GetBytes(typeName));
        writer.Write((ushort)0); // null terminator
    }
}
