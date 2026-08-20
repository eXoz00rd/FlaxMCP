using System.ComponentModel;
using FlaxMcp.Flax;
using ModelContextProtocol.Server;

namespace FlaxMcp.Tools;

[McpServerToolType]
public sealed class EditorTools
{
    private readonly IFlaxBridgeClient _bridgeClient;

    public EditorTools(IFlaxBridgeClient bridgeClient)
    {
        _bridgeClient = bridgeClient;
    }

    [McpServerTool(Name = "editor_scene_graph", ReadOnly = true, UseStructuredContent = true)]
    [Description(
        "Returns the live actor tree from every scene loaded in the Flax Editor, including unsaved runtime state. " +
        "Truncated is true when the 500-node or 32-level response limit is reached."
    )]
    public Task<FlaxLiveSceneGraph> GetSceneGraphAsync(CancellationToken cancellationToken)
    {
        return _bridgeClient.GetSceneGraphAsync(cancellationToken);
    }
}
