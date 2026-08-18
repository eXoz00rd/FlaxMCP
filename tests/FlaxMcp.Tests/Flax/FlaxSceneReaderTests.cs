using System.Text.Json.Nodes;
using FlaxMcp.Configuration;
using FlaxMcp.Flax;
using Xunit;

namespace FlaxMcp.Tests.Flax;

public sealed class FlaxSceneReaderTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "FlaxMcpTests_" + Guid.NewGuid().ToString("N"));

    public FlaxSceneReaderTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void ReadOutline_SeparatesScriptsFromChildActors()
    {
        var path = WriteScene(
            [
                ("root", "FlaxEngine.Scene", "Main", null, false),
                ("script-on-root", "ExitOnEsc", null, "root", true),
                ("child", "FlaxEngine.EmptyActor", "Child", "root", false),
                ("grandchild", "FlaxEngine.EmptyActor", "Grandchild", "child", false),
            ]
        );

        var outline = FlaxSceneReader.ReadOutline(path);

        var root = Assert.Single(outline.Roots);
        Assert.Equal("root", root.Id);
        Assert.Equal(["ExitOnEsc"], root.Scripts);
        var child = Assert.Single(root.Children);
        Assert.Equal("child", child.Id);
        Assert.Empty(child.Scripts);
        var grandchild = Assert.Single(child.Children);
        Assert.Equal("grandchild", grandchild.Id);
        Assert.False(outline.Truncated);
    }

    [Fact]
    public void ReadOutline_ActorWithAnEmptyVProperty_IsStillClassifiedAsAnActor()
    {
        // Regression: real FlaxEngine.UICanvas actors carry both a Name and an empty "V" property.
        // Classifying by V-presence (instead of Name-presence) would wrongly treat them as scripts
        // and drop their subtree.
        var path = WriteScene(
            [
                ("root", "FlaxEngine.Scene", "Main", null, false),
                ("canvas", "FlaxEngine.UICanvas", "Debug UI", "root", true),
                ("control", "FlaxEngine.UIControl", "Perf Counter", "canvas", false),
            ]
        );

        var outline = FlaxSceneReader.ReadOutline(path);

        var root = Assert.Single(outline.Roots);
        var canvas = Assert.Single(root.Children);
        Assert.Equal("Debug UI", canvas.Name);
        var control = Assert.Single(canvas.Children);
        Assert.Equal("Perf Counter", control.Name);
    }

    [Fact]
    public void ReadOutline_BuildsTreeFromParentIdRegardlessOfArrayOrder()
    {
        var path = WriteScene(
            [
                ("grandchild", "FlaxEngine.EmptyActor", "Grandchild", "child", false),
                ("child", "FlaxEngine.EmptyActor", "Child", "root", false),
                ("root", "FlaxEngine.Scene", "Main", null, false),
            ]
        );

        var outline = FlaxSceneReader.ReadOutline(path);

        var root = Assert.Single(outline.Roots);
        var child = Assert.Single(root.Children);
        var grandchild = Assert.Single(child.Children);
        Assert.Equal("grandchild", grandchild.Id);
    }

    [Fact]
    public void ReadOutline_WithDanglingParentId_TreatsItAsAnotherRoot()
    {
        var path = WriteScene(
            [
                ("root", "FlaxEngine.Scene", "Main", null, false),
                ("orphan", "FlaxEngine.EmptyActor", "Orphan", "does-not-exist", false),
            ]
        );

        var outline = FlaxSceneReader.ReadOutline(path);

        Assert.Equal(2, outline.Roots.Count);
        Assert.Contains(outline.Roots, a => a.Id == "orphan");
    }

    [Fact]
    public void ReadOutline_WithDuplicateActorId_KeepsFirstOccurrenceWithoutThrowing()
    {
        var path = WriteScene(
            [
                ("root", "FlaxEngine.Scene", "Main", null, false),
                ("dup", "FlaxEngine.EmptyActor", "First", "root", false),
                ("dup", "FlaxEngine.EmptyActor", "Second", "root", false),
            ]
        );

        var outline = FlaxSceneReader.ReadOutline(path);

        var root = Assert.Single(outline.Roots);
        var child = Assert.Single(root.Children);
        Assert.Equal("First", child.Name);
    }

    [Fact]
    public void ReadOutline_DeeperThanMaxDepth_IsTruncated()
    {
        var entries = new List<(string, string, string?, string?, bool)> { ("actor-0", "FlaxEngine.EmptyActor", "0", null, false) };
        for (var i = 1; i <= ResponseLimits.DefaultMaxDepth + 5; i++)
        {
            entries.Add(($"actor-{i}", "FlaxEngine.EmptyActor", i.ToString(), $"actor-{i - 1}", false));
        }
        var path = WriteScene(entries);

        var outline = FlaxSceneReader.ReadOutline(path);

        Assert.True(outline.Truncated);
        var depth = 0;
        var node = Assert.Single(outline.Roots);
        while (node.Children.Count > 0)
        {
            node = Assert.Single(node.Children);
            depth++;
        }
        Assert.Equal(ResponseLimits.DefaultMaxDepth, depth);
    }

    [Fact]
    public void ReadOutline_MoreNodesThanMaxItems_IsTruncated()
    {
        var entries = new List<(string, string, string?, string?, bool)> { ("root", "FlaxEngine.Scene", "Main", null, false) };
        for (var i = 0; i < ResponseLimits.DefaultMaxItems + 20; i++)
        {
            entries.Add(($"child-{i}", "FlaxEngine.EmptyActor", $"Child{i}", "root", false));
        }
        var path = WriteScene(entries);

        var outline = FlaxSceneReader.ReadOutline(path);

        Assert.True(outline.Truncated);
        var root = Assert.Single(outline.Roots);
        Assert.Equal(ResponseLimits.DefaultMaxItems - 1, root.Children.Count);
    }

    [Fact]
    public void ReadOutline_WithMissingFile_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => FlaxSceneReader.ReadOutline(Path.Combine(_tempDir, "Missing.scene")));
    }

    [Fact]
    public void ReadOutline_WithMalformedJson_ThrowsInvalidOperationException()
    {
        var path = Path.Combine(_tempDir, "Malformed.scene");
        File.WriteAllText(path, "{ not valid json");

        Assert.Throws<InvalidOperationException>(() => FlaxSceneReader.ReadOutline(path));
    }

    [Fact]
    public void FindActors_FiltersByPartialNameAndExactTypeName()
    {
        var path = WriteScene(
            [
                ("root", "FlaxEngine.Scene", "Main", null, false),
                ("cam1", "FlaxEngine.Camera", "Free Camera", "root", false),
                ("cam2", "FlaxEngine.Camera", "Isometric Camera", "root", false),
                ("light", "FlaxEngine.SkyLight", "SkyLight", "root", false),
                ("script", "FreeCamera", null, "cam1", true),
            ]
        );

        var byName = FlaxSceneReader.FindActors(path, name: "Camera", typeName: null);
        Assert.Equal(2, byName.Count);

        var byType = FlaxSceneReader.FindActors(path, name: null, typeName: "FlaxEngine.SkyLight");
        var match = Assert.Single(byType);
        Assert.Equal("light", match.Id);
    }

    private string WriteScene(IEnumerable<(string Id, string TypeName, string? Name, string? ParentId, bool IsScript)> entries)
    {
        var dataArray = new JsonArray();
        foreach (var (id, typeName, name, parentId, isScript) in entries)
        {
            var entry = new JsonObject { ["ID"] = id, ["TypeName"] = typeName };
            if (name is not null)
            {
                entry["Name"] = name;
            }
            if (parentId is not null)
            {
                entry["ParentID"] = parentId;
            }
            if (isScript)
            {
                entry["V"] = new JsonObject();
            }
            dataArray.Add(entry);
        }

        var document = new JsonObject
        {
            ["ID"] = "scene-id",
            ["TypeName"] = "FlaxEngine.SceneAsset",
            ["EngineBuild"] = 6910,
            ["Data"] = dataArray,
        };

        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".scene");
        File.WriteAllText(path, document.ToJsonString());
        return path;
    }
}
