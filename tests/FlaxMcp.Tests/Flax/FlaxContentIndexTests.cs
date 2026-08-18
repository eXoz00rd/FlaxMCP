using FlaxMcp.Configuration;
using FlaxMcp.Flax;
using Microsoft.Extensions.Options;
using Xunit;

namespace FlaxMcp.Tests.Flax;

public sealed class FlaxContentIndexTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "FlaxMcpTests_" + Guid.NewGuid().ToString("N"));
    private readonly string _contentDir;
    private readonly FlaxContentIndex _index;

    public FlaxContentIndexTests()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(Path.Combine(_tempDir, "Game.flaxproj"), """{ "Name": "Game" }""");

        _contentDir = Path.Combine(_tempDir, "Content");
        Directory.CreateDirectory(Path.Combine(_contentDir, "Scenes"));
        Directory.CreateDirectory(Path.Combine(_contentDir, "Materials"));

        File.WriteAllText(
            Path.Combine(_contentDir, "GameSettings.json"),
            """{ "ID": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "TypeName": "FlaxEditor.Content.Settings.GameSettings", "Data": {} }"""
        );
        File.WriteAllText(
            Path.Combine(_contentDir, "Scenes", "Main.scene"),
            """{ "ID": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "TypeName": "FlaxEngine.SceneAsset", "Data": [] }"""
        );
        WriteFlaxHeader(
            Path.Combine(_contentDir, "Materials", "Floor Material.flax"),
            "CFWF"u8.ToArray(),
            [0x26, 0x75, 0xBF, 0xC5, 0x20, 0x8F, 0xC0, 0x4A, 0xA5, 0xEE, 0x16, 0x5D, 0x6E, 0xD1, 0xE3, 0x54],
            "FlaxEngine.Material"
        );
        WriteFlaxHeader(
            Path.Combine(_contentDir, "Materials", "Garbage.flax"),
            "XXXX"u8.ToArray(),
            new byte[16],
            "Unused"
        );

        _index = new FlaxContentIndex(Options.Create(new FlaxMcpOptions { ProjectPath = _tempDir }));
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Search_WithNoFilters_IndexesEveryFileIncludingUnparseableOnes()
    {
        var results = _index.Search(query: null, typeName: null, extension: null);

        Assert.Equal(4, results.Count);
        var garbage = Assert.Single(results, a => a.RelativePath == "Materials/Garbage.flax");
        Assert.Null(garbage.Id);
        Assert.Null(garbage.TypeName);
    }

    [Fact]
    public void Search_ByPartialPath_FindsMatchingAsset()
    {
        var results = _index.Search(query: "Floor", typeName: null, extension: null);

        var match = Assert.Single(results);
        Assert.Equal("Materials/Floor Material.flax", match.RelativePath);
    }

    [Fact]
    public void Search_ByTypeName_FindsMatchingAsset()
    {
        var results = _index.Search(query: null, typeName: "FlaxEngine.SceneAsset", extension: null);

        var match = Assert.Single(results);
        Assert.Equal("Scenes/Main.scene", match.RelativePath);
    }

    [Fact]
    public void Search_ByExtension_FindsMatchingAssets()
    {
        var results = _index.Search(query: null, typeName: null, extension: ".flax");

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void GetById_ResolvesRealAsset()
    {
        var asset = _index.GetById("c5bf75264ac08f205d16eea554e3d16e");

        Assert.NotNull(asset);
        Assert.Equal("Materials/Floor Material.flax", asset.RelativePath);
    }

    [Fact]
    public void GetById_WithUnknownId_ReturnsNull()
    {
        var asset = _index.GetById("00000000000000000000000000000000");

        Assert.Null(asset);
    }

    [Fact]
    public void Search_WithNonStringIdField_StillIndexesFileWithNullMetadata()
    {
        File.WriteAllText(
            Path.Combine(_contentDir, "BadId.json"),
            """{ "ID": 12345, "TypeName": "FlaxEngine.Whatever", "Data": {} }"""
        );

        var results = _index.Search(query: "BadId", typeName: null, extension: null);

        var match = Assert.Single(results);
        Assert.Null(match.Id);
        Assert.Null(match.TypeName);
    }

    private static void WriteFlaxHeader(string filePath, byte[] magic, byte[] guidBytes, string typeName)
    {
        using var stream = File.Create(filePath);
        using var writer = new BinaryWriter(stream);

        writer.Write(magic);
        writer.Write(9);
        writer.Write(new byte[16]);
        writer.Write(1);
        writer.Write(guidBytes);
        writer.Write(System.Text.Encoding.Unicode.GetBytes(typeName));
        writer.Write((ushort)0);
    }
}
