using FlaxMcp.Flax;
using Xunit;

namespace FlaxMcp.Tests.Flax;

public sealed class FlaxProjectSettingsReaderTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "FlaxMcpTests_" + Guid.NewGuid().ToString("N"));

    public FlaxProjectSettingsReaderTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void ReadAll_CollectsSettingsFromContentRootAndSettingsSubdirectory()
    {
        var contentDir = Path.Combine(_tempDir, "Content");
        var settingsDir = Path.Combine(contentDir, "Settings");
        Directory.CreateDirectory(settingsDir);

        File.WriteAllText(
            Path.Combine(contentDir, "GameSettings.json"),
            """
            {
                "ID": "9eba3d2a4cee4c099117f49c5dffc171",
                "TypeName": "FlaxEditor.Content.Settings.GameSettings",
                "EngineBuild": 6910,
                "Data": { "ProductName": "Mournfall" }
            }
            """
        );
        File.WriteAllText(
            Path.Combine(settingsDir, "Graphics Settings.json"),
            """
            {
                "ID": "4eaf325e4cd72aa4cdeb6393cad05466",
                "TypeName": "FlaxEditor.Content.Settings.GraphicsSettings",
                "EngineBuild": 6705,
                "Data": { "UseVSync": true }
            }
            """
        );

        var settings = FlaxProjectSettingsReader.ReadAll(_tempDir);

        Assert.Equal(2, settings.Count);

        var game = Assert.Single(settings, s => s.Name == "GameSettings");
        Assert.Equal("FlaxEditor.Content.Settings.GameSettings", game.TypeName);
        Assert.Equal("Mournfall", game.Data?["ProductName"]?.GetValue<string>());

        var graphics = Assert.Single(settings, s => s.Name == "Graphics Settings");
        Assert.Equal("FlaxEditor.Content.Settings.GraphicsSettings", graphics.TypeName);
        Assert.True(graphics.Data?["UseVSync"]?.GetValue<bool>());
    }

    [Fact]
    public void ReadAll_WithNoContentDirectory_ReturnsEmpty()
    {
        var settings = FlaxProjectSettingsReader.ReadAll(_tempDir);

        Assert.Empty(settings);
    }

    [Fact]
    public void ReadAll_SkipsFilesWithoutADataProperty()
    {
        var contentDir = Path.Combine(_tempDir, "Content");
        Directory.CreateDirectory(contentDir);
        File.WriteAllText(Path.Combine(contentDir, "NotSettings.json"), """{ "SomeField": 1 }""");

        var settings = FlaxProjectSettingsReader.ReadAll(_tempDir);

        Assert.Empty(settings);
    }
}
