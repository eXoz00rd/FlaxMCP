using FlaxMcp.Configuration;
using FlaxMcp.Flax;
using FlaxMcp.Tools;
using Microsoft.Extensions.Options;
using Xunit;

namespace FlaxMcp.Tests.Tools;

public sealed class SceneToolsTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "FlaxMcpTests_" + Guid.NewGuid().ToString("N"));
    private readonly SceneTools _tool;

    public SceneToolsTests()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(Path.Combine(_tempDir, "Game.flaxproj"), """{ "Name": "Game" }""");
        var scenesDir = Path.Combine(_tempDir, "Content", "Scenes");
        Directory.CreateDirectory(scenesDir);
        File.WriteAllText(
            Path.Combine(scenesDir, "Main.scene"),
            """
            {
                "ID": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                "TypeName": "FlaxEngine.SceneAsset",
                "EngineBuild": 6910,
                "Data": [
                    { "ID": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "TypeName": "FlaxEngine.Scene", "Name": "Main" },
                    { "ID": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "TypeName": "FlaxEngine.SkyLight", "Name": "SkyLight", "ParentID": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" }
                ]
            }
            """
        );

        var options = Options.Create(new FlaxMcpOptions { ProjectPath = _tempDir });
        _tool = new SceneTools(new FlaxContentIndex(options), options);
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void ListScenes_FindsSceneFile()
    {
        var scenes = _tool.ListScenes();

        var match = Assert.Single(scenes);
        Assert.Equal("Scenes/Main.scene", match.RelativePath);
    }

    [Fact]
    public void GetSceneOutline_ReturnsActorTree()
    {
        var outline = _tool.GetSceneOutline("Scenes/Main.scene");

        var root = Assert.Single(outline.Roots);
        Assert.Equal("Main", root.Name);
        var child = Assert.Single(root.Children);
        Assert.Equal("SkyLight", child.Name);
    }

    [Fact]
    public void GetSceneOutline_WithMissingPath_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => _tool.GetSceneOutline("Scenes/DoesNotExist.scene"));
    }

    [Fact]
    public void FindActor_FiltersByName()
    {
        var results = _tool.FindActor("Scenes/Main.scene", name: "Sky");

        var match = Assert.Single(results);
        Assert.Equal("SkyLight", match.Name);
    }

    [Fact]
    public void FindActor_WithMissingPath_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => _tool.FindActor("Scenes/DoesNotExist.scene"));
    }
}
