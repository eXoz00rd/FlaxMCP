using System.Text;
using System.Text.RegularExpressions;

namespace FlaxMcp.Flax;

/// <summary>
/// Reads a Flax project's <c>Logs/*.txt</c> files. Real files carry a genuine UTF-16LE byte-order
/// mark (<c>0xFF 0xFE</c>) — verified against raw bytes from a live <c>FlaxEditor.exe</c> run, despite
/// this format commonly being described as BOM-less. <see cref="StreamReader"/>'s BOM auto-detection
/// handles both cases, so decoding here doesn't hard-code either assumption.
/// </summary>
public static partial class FlaxLogReader
{
    public static FileInfo? FindLatestLogFile(string projectFolder, DateTime? afterUtc = null)
    {
        var logsDirectory = Path.Combine(projectFolder, "Logs");
        if (!Directory.Exists(logsDirectory))
        {
            return null;
        }

        return new DirectoryInfo(logsDirectory)
               .EnumerateFiles("*.txt")
               .Where(file => afterUtc is null || file.LastWriteTimeUtc >= afterUtc)
               .OrderByDescending(file => file.LastWriteTimeUtc)
               .FirstOrDefault();
    }

    public static string ReadLog(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete
        );
        using var reader = new StreamReader(stream, Encoding.Unicode, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Parses the engine's own <c>Total errors: N</c> summary line. This is the only reliable
    /// success/failure signal for a headless run — <c>FlaxEditor.exe</c>'s process exit code stays 0
    /// even when script compilation fails, verified by running headless against a project with a
    /// deliberately broken script. Detailed per-error parsing (file/line/code/message) is out of
    /// scope here; that's for the build tools that consume this summary.
    /// </summary>
    public static int? TryParseTotalErrors(string log)
    {
        var match = TotalErrorsPattern().Match(log);
        return match.Success ? int.Parse(match.Groups[1].Value) : null;
    }

    [GeneratedRegex(@"Total errors:\s*(\d+)")]
    private static partial Regex TotalErrorsPattern();
}
