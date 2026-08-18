namespace FlaxMcp.Flax.Models;

public sealed record FlaxProjectTargetsInfo(
    FlaxBuildTargetInfo GameTarget,
    FlaxBuildTargetInfo EditorTarget
);
