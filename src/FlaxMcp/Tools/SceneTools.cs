using System.ComponentModel;
using FlaxMcp.Configuration;
using FlaxMcp.Flax;
using FlaxMcp.Flax.Models;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace FlaxMcp.Tools;

[McpServerToolType]
public sealed class SceneTools
{
    private readonly FlaxContentIndex _index;
    private readonly IOptions<FlaxMcpOptions> _options;

    public SceneTools(FlaxContentIndex index, IOptions<FlaxMcpOptions> options)
    {
        _index = index;
        _options = options;
    }

    [McpServerTool(Name = "scene_list", ReadOnly = true, UseStructuredContent = true)]
    [Description("Lists scenes (.scene) and prefabs (.prefab) in the project's Content/ directory.")]
    public IReadOnlyList<FlaxContentAssetInfo> ListScenes()
    {
        return [.. _index.Search(query: null, typeName: null, extension: ".scene"), .. _index.Search(query: null, typeName: null, extension: ".prefab")];
    }

    [McpServerTool(Name = "scene_outline", ReadOnly = true, UseStructuredContent = true)]
    [Description("Reads a .scene/.prefab file's actor tree, built from ParentID linkage (not file order). Scripts attached to an actor are listed on that actor's Scripts, not as child nodes. Truncated is true if the configured depth/node-count limit was hit.")]
    public FlaxSceneOutline GetSceneOutline(string path)
    {
        return FlaxSceneReader.ReadOutline(ResolveContentPath(path));
    }

    [McpServerTool(Name = "scene_find_actor", ReadOnly = true, UseStructuredContent = true)]
    [Description("Searches a .scene/.prefab file's actors by partial Name match and/or exact TypeName.")]
    public IReadOnlyList<FlaxSceneActorInfo> FindActor(string path, string? name = null, string? typeName = null)
    {
        return FlaxSceneReader.FindActors(ResolveContentPath(path), name, typeName);
    }

    private string ResolveContentPath(string path)
    {
        var fullPath = Path.Combine(_options.Value.ResolveContentDirectory(), path);
        if (!File.Exists(fullPath))
        {
            throw new InvalidOperationException($"'{path}' does not exist under the project's Content/ directory.");
        }
        return fullPath;
    }
}
