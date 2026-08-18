namespace FlaxMcp.Flax.Models;

public sealed record FlaxSceneActorNode(
    string Id,
    string TypeName,
    string? Name,
    IReadOnlyList<string> Scripts,
    IReadOnlyList<FlaxSceneActorNode> Children
);
