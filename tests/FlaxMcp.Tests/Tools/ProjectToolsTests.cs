using FlaxMcp.Configuration;
using FlaxMcp.Tools;
using Microsoft.Extensions.Options;
using Xunit;

namespace FlaxMcp.Tests.Tools;

public sealed class ProjectToolsTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "FlaxMcpTests_" + Guid.NewGuid().ToString("N"));

    public ProjectToolsTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void GetProjectInfo_ResolvesAndReadsTheProjectFile()
    {
        File.WriteAllText(Path.Combine(_tempDir, "Game.flaxproj"), """{ "Name": "Game", "Version": "2.0" }""");
        var tool = new ProjectTools(Options.Create(new FlaxMcpOptions { ProjectPath = _tempDir }));

        var info = tool.GetProjectInfo();

        Assert.Equal("Game", info.Name);
        Assert.Equal("2.0", info.Version);
    }

    [Fact]
    public void GetProjectTargets_ReadsBothTargetBuildFiles()
    {
        File.WriteAllText(
            Path.Combine(_tempDir, "Game.flaxproj"),
            """{ "Name": "Game", "GameTarget": "GameTarget", "EditorTarget": "GameEditorTarget" }"""
        );
        var sourceDir = Path.Combine(_tempDir, "Source");
        Directory.CreateDirectory(sourceDir);
        File.WriteAllText(
            Path.Combine(sourceDir, "GameTarget.Build.cs"),
            """
            public class GameTarget : GameProjectTarget
            {
                public override void Init()
                {
                    base.Init();
                    Modules.Add("Game");
                }
            }
            """
        );
        File.WriteAllText(
            Path.Combine(sourceDir, "GameEditorTarget.Build.cs"),
            """
            public class GameEditorTarget : GameProjectEditorTarget
            {
                public override void Init()
                {
                    base.Init();
                    Modules.Add("Game");
                }
            }
            """
        );
        var tool = new ProjectTools(Options.Create(new FlaxMcpOptions { ProjectPath = _tempDir }));

        var targets = tool.GetProjectTargets();

        Assert.Equal("GameTarget", targets.GameTarget.Name);
        Assert.Equal(["Game"], targets.GameTarget.Modules);
        Assert.Equal("GameEditorTarget", targets.EditorTarget.Name);
        Assert.Equal(["Game"], targets.EditorTarget.Modules);
    }

    [Fact]
    public void GetProjectTargets_WithMissingTargetNames_ThrowsClearError()
    {
        File.WriteAllText(Path.Combine(_tempDir, "Game.flaxproj"), """{ "Name": "Game" }""");
        var tool = new ProjectTools(Options.Create(new FlaxMcpOptions { ProjectPath = _tempDir }));

        var exception = Assert.Throws<InvalidOperationException>(() => tool.GetProjectTargets());
        Assert.Contains("GameTarget", exception.Message);
        Assert.Contains("EditorTarget", exception.Message);
    }

    [Fact]
    public void GetProjectSettings_ReadsSettingsFromContentDirectory()
    {
        File.WriteAllText(Path.Combine(_tempDir, "Game.flaxproj"), """{ "Name": "Game" }""");
        var contentDir = Path.Combine(_tempDir, "Content");
        Directory.CreateDirectory(contentDir);
        File.WriteAllText(
            Path.Combine(contentDir, "GameSettings.json"),
            """
            {
                "ID": "9eba3d2a4cee4c099117f49c5dffc171",
                "TypeName": "FlaxEditor.Content.Settings.GameSettings",
                "EngineBuild": 6910,
                "Data": { "ProductName": "Game" }
            }
            """
        );
        var tool = new ProjectTools(Options.Create(new FlaxMcpOptions { ProjectPath = _tempDir }));

        var settings = tool.GetProjectSettings();

        var gameSettings = Assert.Single(settings);
        Assert.Equal("GameSettings", gameSettings.Name);
        Assert.Equal("Game", gameSettings.Data?["ProductName"]?.GetValue<string>());
    }
}
