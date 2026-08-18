namespace FlaxMcp.Flax.Models;

public sealed record FlaxProjectInfo(
    string Name,
    string Version,
    string GameTarget,
    string EditorTarget,
    IReadOnlyList<string> References,
    string? DefaultScene,
    string? MinEngineVersion
);
