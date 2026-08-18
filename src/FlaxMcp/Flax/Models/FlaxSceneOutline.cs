namespace FlaxMcp.Flax.Models;

public sealed record FlaxSceneOutline(
    string Id,
    int? EngineBuild,
    IReadOnlyList<FlaxSceneActorNode> Roots,
    bool Truncated
);
