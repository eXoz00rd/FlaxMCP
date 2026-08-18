namespace FlaxMcp.Flax.Models;

public sealed record FlaxBuildTargetInfo(
    string Name,
    string BaseClass,
    IReadOnlyList<string> Modules
);
