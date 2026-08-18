namespace FlaxMcp.Flax.Models;

public sealed record FlaxSceneActorInfo(
    string Id,
    string TypeName,
    string? Name,
    string? ParentId
);
