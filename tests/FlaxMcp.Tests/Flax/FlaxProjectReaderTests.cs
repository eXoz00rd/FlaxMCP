using FlaxMcp.Flax;
using Xunit;

namespace FlaxMcp.Tests.Flax;

public sealed class FlaxProjectReaderTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "FlaxMcpTests_" + Guid.NewGuid().ToString("N"));

    public FlaxProjectReaderTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Read_ParsesRealisticFlaxprojDocument()
    {
        var projectFile = Path.Combine(_tempDir, "Mournfall.flaxproj");
        File.WriteAllText(
            projectFile,
            """
            {
            	"Name": "Mournfall",
            	"Version": "1.0",
            	"GameTarget": "GameTarget",
            	"EditorTarget": "GameEditorTarget",
            	"References": [
            		{ "Name": "$(EnginePath)/Flax.flaxproj" }
            	],
            	"DefaultScene": "a470726f441936acfe25318b162c336c",
            	"MinEngineVersion": "1.12.6912"
            }
            """
        );

        var info = FlaxProjectReader.Read(projectFile);

        Assert.Equal("Mournfall", info.Name);
        Assert.Equal("1.0", info.Version);
        Assert.Equal("GameTarget", info.GameTarget);
        Assert.Equal("GameEditorTarget", info.EditorTarget);
        Assert.Equal(["$(EnginePath)/Flax.flaxproj"], info.References);
        Assert.Equal("a470726f441936acfe25318b162c336c", info.DefaultScene);
        Assert.Equal("1.12.6912", info.MinEngineVersion);
    }

    [Fact]
    public void Read_WithMissingOptionalFields_FillsSensibleDefaults()
    {
        var projectFile = Path.Combine(_tempDir, "Minimal.flaxproj");
        File.WriteAllText(projectFile, "{}");

        var info = FlaxProjectReader.Read(projectFile);

        Assert.Equal("Minimal", info.Name);
        Assert.Equal("0.0", info.Version);
        Assert.Empty(info.References);
        Assert.Null(info.DefaultScene);
        Assert.Null(info.MinEngineVersion);
    }
}
