using System.ComponentModel;
using FlaxMcp.Configuration;
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

    [McpServerTool(Name = "editor_modify_actor", UseStructuredContent = true)]
    [Description(
        "Replaces an existing live actor's world transform and returns its updated details. " +
        "Use editor_actor_details first to preserve transform values that should not change."
    )]
    public Task<FlaxActorDetails> ModifyActorAsync(
        [Description("Actor GUID from editor_scene_graph or editor_get_selection.")] string actorId,
        [Description("Replacement world-space translation.")]
        FlaxVector3 translation,
        [Description("Replacement world-space orientation quaternion.")]
        FlaxQuaternion orientation,
        [Description("Replacement world-space scale.")]
        FlaxVector3 scale,
        CancellationToken cancellationToken)
    {
        return _bridgeClient.ModifyActorAsync(
            actorId,
            new FlaxActorTransform(translation, orientation, scale),
            cancellationToken
        );
    }

    [McpServerTool(Name = "editor_create_actor", UseStructuredContent = true)]
    [Description("Creates an allowlisted EmptyActor or StaticModel in a loaded scene and returns its live details.")]
    public Task<FlaxActorDetails> CreateActorAsync(
        [Description("Allowlisted actor type: EmptyActor or StaticModel.")] string actorType,
        string name,
        string? sceneId,
        string? parentId,
        FlaxVector3 translation,
        FlaxQuaternion orientation,
        FlaxVector3 scale,
        CancellationToken cancellationToken)
    {
        return _bridgeClient.CreateActorAsync(actorType, name, sceneId, parentId,
            new FlaxActorTransform(translation, orientation, scale), cancellationToken);
    }

    [McpServerTool(Name = "editor_duplicate_actor", UseStructuredContent = true)]
    [Description("Duplicates a loaded actor into a loaded scene or parent and returns the duplicate's live details.")]
    public Task<FlaxActorDetails> DuplicateActorAsync(
        string actorId,
        string name,
        string? sceneId,
        string? parentId,
        FlaxVector3 translation,
        FlaxQuaternion orientation,
        FlaxVector3 scale,
        CancellationToken cancellationToken)
    {
        return _bridgeClient.DuplicateActorAsync(actorId, name, sceneId, parentId,
            new FlaxActorTransform(translation, orientation, scale), cancellationToken);
    }

    [McpServerTool(Name = "editor_rename_actor", UseStructuredContent = true)]
    [Description("Renames a loaded actor through the editor Undo/Redo history and returns its updated details.")]
    public Task<FlaxActorDetails> RenameActorAsync(
        [Description("Actor GUID from editor_scene_graph.")] string actorId,
        [Description("New non-empty actor name.")] string name,
        CancellationToken cancellationToken)
    {
        return _bridgeClient.RenameActorAsync(actorId, name, cancellationToken);
    }

    [McpServerTool(Name = "editor_reparent_actor", UseStructuredContent = true)]
    [Description(
        "Moves a loaded actor to another loaded parent or to a loaded scene root through Undo/Redo history. " +
        "Set exactly one of sceneId or parentId and explicitly choose whether to preserve the world transform."
    )]
    public Task<FlaxActorDetails> ReparentActorAsync(
        [Description("Actor GUID from editor_scene_graph.")] string actorId,
        string? sceneId,
        string? parentId,
        bool preserveWorldTransform,
        CancellationToken cancellationToken)
    {
        return _bridgeClient.ReparentActorAsync(
            actorId, sceneId, parentId, preserveWorldTransform, cancellationToken);
    }

    [McpServerTool(Name = "editor_save", UseStructuredContent = true)]
    [Description("Saves all modified scenes and content assets in the live Flax Editor.")]
    public Task<FlaxEditorSaveResult> SaveAsync(CancellationToken cancellationToken)
    {
        return _bridgeClient.SaveAsync(cancellationToken);
    }

    [McpServerTool(Name = "editor_play_mode", UseStructuredContent = true)]
    [Description(
        "Controls play mode in the live Flax Editor. Action must be start, stop, pause, or resume."
    )]
    public Task<FlaxEditorPlayModeResult> SetPlayModeAsync(
        [Description("Play mode action: start, stop, pause, or resume.")] string action,
        CancellationToken cancellationToken)
    {
        return _bridgeClient.SetPlayModeAsync(action, cancellationToken);
    }

    [McpServerTool(Name = "editor_screenshot", UseStructuredContent = true)]
    [Description(
        "Captures the visible Flax Editor scene viewport to a PNG file. " +
        "The output directory must already exist, and capture is unavailable in headless mode."
    )]
    public Task<FlaxBridgeScreenshot> CaptureScreenshotAsync(
        [Description("Absolute or current-working-directory-relative output path ending in .png.")] string path,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var resolvedPath = Path.GetFullPath(path);
        if (!string.Equals(Path.GetExtension(resolvedPath), ".png", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Screenshot output path must use the .png extension.", nameof(path));
        }

        var directory = Path.GetDirectoryName(resolvedPath)!;
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Screenshot output directory does not exist: {directory}");
        }

        return _bridgeClient.CaptureScreenshotAsync(resolvedPath, cancellationToken);
    }

    [McpServerTool(Name = "editor_execute_csharp", UseStructuredContent = true)]
    [RequiresCodeExecution]
    [Description(
        "DANGER: Compiles and executes arbitrary C# with the Flax Editor process's full machine permissions. " +
        "The code is the body of a static method, runs on the editor main thread, and must return a JSON-serializable value."
    )]
    public Task<FlaxCodeExecutionResult> ExecuteCSharpAsync(
        [Description("C# method body, for example: return FlaxEngine.Globals.EngineBuildNumber;")] string code,
        CancellationToken cancellationToken)
    {
        return _bridgeClient.ExecuteCSharpAsync(code, cancellationToken);
    }
}
