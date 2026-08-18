namespace FlaxMcp.Flax.Models;

/// <summary>
/// Outcome of an external process launched by <see cref="FlaxProcessRunner"/>. For a headless
/// <c>FlaxEditor.exe</c> run, <see cref="StandardOutput"/>/<see cref="StandardError"/> are
/// best-effort only — the engine's log goes to <c>Logs/*.txt</c>, not reliably to stdout when
/// launched detached (see <see cref="FlaxLogReader"/>).
/// </summary>
public sealed record FlaxProcessResult(int? ExitCode, bool TimedOut, string StandardOutput, string StandardError);
