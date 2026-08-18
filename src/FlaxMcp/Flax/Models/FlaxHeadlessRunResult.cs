namespace FlaxMcp.Flax.Models;

/// <summary>
/// Outcome of a headless <c>FlaxEditor.exe</c> run: the process's exit code, the engine log written
/// during the run (from <c>Logs/*.txt</c>, not from captured stdout), and <see cref="ErrorCount"/>
/// parsed from the log's own "Total errors: N" summary. <see cref="ExitCode"/> alone cannot detect a
/// failed script compile — the process still exits 0 in that case — so <see cref="ErrorCount"/> is
/// the signal to check for compile/build failures.
/// </summary>
public sealed record FlaxHeadlessRunResult(int? ExitCode, bool TimedOut, string? Log, int? ErrorCount);
