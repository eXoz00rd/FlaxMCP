namespace FlaxMcp.Flax.Models;

/// <summary>
/// The tail of a project's most recent <c>Logs/*.txt</c> file. <see cref="FileName"/> is included
/// because Flax writes a new log file per session -- without it, a caller can't tell whether the
/// lines are from the run they care about or a stale earlier one.
/// </summary>
public sealed record FlaxLogTail(string FileName, IReadOnlyList<string> Lines);
