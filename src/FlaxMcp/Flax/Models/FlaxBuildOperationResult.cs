namespace FlaxMcp.Flax.Models;

/// <summary>
/// Outcome of a headless build-related operation (project generation, script compilation, cache
/// clearing, or a completed <c>build_game</c> job). <see cref="Succeeded"/> folds in
/// <see cref="ErrorCount"/>, not just <see cref="ExitCode"/> -- <c>FlaxEditor.exe</c>'s process exit
/// code stays 0 even when script compilation fails (see <see cref="FlaxMcp.Flax.FlaxHeadlessEditorRunner"/>).
/// <see cref="Errors"/> holds compiler diagnostics parsed from the log, capped at
/// <see cref="FlaxMcp.Configuration.ResponseLimits.DefaultListTop"/>; it is empty for operations that
/// don't compile scripts.
/// </summary>
public sealed record FlaxBuildOperationResult(
    bool Succeeded,
    int? ExitCode,
    bool TimedOut,
    int? ErrorCount,
    IReadOnlyList<FlaxCompilerDiagnostic> Errors
);
