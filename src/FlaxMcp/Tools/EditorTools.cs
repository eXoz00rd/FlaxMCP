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

    [McpServerTool(Name = "editor_get_selection", ReadOnly = true, UseStructuredContent = true)]
    [Description("Returns the actors currently selected in the live Flax Editor.")]
    public Task<FlaxEditorSelection> GetSelectionAsync(CancellationToken cancellationToken)
    {
        return _bridgeClient.GetSelectionAsync(cancellationToken);
    }

    [McpServerTool(Name = "editor_set_selection", UseStructuredContent = true)]
    [Description(
        "Replaces the live Flax Editor actor selection with the actors identified by GUID. " +
        "Pass an empty list to clear the selection."
    )]
    public Task<FlaxEditorSelection> SetSelectionAsync(
        [Description("Actor GUIDs from editor_scene_graph.")] IReadOnlyList<string> actorIds,
        CancellationToken cancellationToken)
    {
        return _bridgeClient.SetSelectionAsync(actorIds, cancellationToken);
    }

    [McpServerTool(Name = "editor_actor_details", ReadOnly = true, UseStructuredContent = true)]
    [Description(
        "Returns live identity, hierarchy, activation, layer, tags, world/local transforms, and scripts " +
        "for one actor loaded in the Flax Editor."
    )]
    public Task<FlaxActorDetails> GetActorDetailsAsync(
        [Description("Actor GUID from editor_scene_graph or editor_get_selection.")] string actorId,
        CancellationToken cancellationToken)
    {
        return _bridgeClient.GetActorDetailsAsync(actorId, cancellationToken);
    }
}
