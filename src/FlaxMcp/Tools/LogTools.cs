using System.ComponentModel;
using FlaxMcp.Configuration;
using FlaxMcp.Flax;
using FlaxMcp.Flax.Models;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace FlaxMcp.Tools;

[McpServerToolType]
public sealed class LogTools
{
    private readonly IOptions<FlaxMcpOptions> _options;

    public LogTools(IOptions<FlaxMcpOptions> options)
    {
        _options = options;
    }

    [McpServerTool(Name = "logs_tail", ReadOnly = true, UseStructuredContent = true)]
    [Description("Returns the last N lines (default 200) of the project's most recent Logs/*.txt file, without running the editor.")]
    public FlaxLogTail GetLogsTail(int lines = 200)
    {
        var (fileName, logLines) = ReadLatestLog();
        var take = Math.Clamp(lines, 1, ResponseLimits.DefaultMaxItems);
        return new FlaxLogTail(fileName, [.. logLines.Skip(Math.Max(0, logLines.Length - take))]);
    }

    [McpServerTool(Name = "logs_errors", ReadOnly = true, UseStructuredContent = true)]
    [Description("Extracts lines tagged [Error] or [Warning] from the project's most recent Logs/*.txt file, without running the editor.")]
    public IReadOnlyList<string> GetLogErrors()
    {
        var (_, logLines) = ReadLatestLog();
        return [
            .. logLines
               .Where(line => line.Contains("[Error]", StringComparison.Ordinal) || line.Contains("[Warning]", StringComparison.Ordinal))
               .Take(ResponseLimits.DefaultListTop),
        ];
    }

    private (string FileName, string[] Lines) ReadLatestLog()
    {
        var projectFolder = Path.GetDirectoryName(_options.Value.ResolveProjectFile())!;
        var logFile = FlaxLogReader.FindLatestLogFile(projectFolder) ??
            throw new InvalidOperationException($"No log files found under '{Path.Combine(projectFolder, "Logs")}'.");

        var log = FlaxLogReader.ReadLog(logFile.FullName);
        // Real Flax logs are CRLF-terminated (verified against a live run); splitting on "\r\n"/"\n"
        // handles both. A file's trailing newline produces one spurious empty entry at the end -- drop
        // just that one rather than every blank line, so genuine blank lines mid-log survive.
        var lines = log.Split(["\r\n", "\n"], StringSplitOptions.None);
        if (lines.Length > 0 && lines[^1].Length == 0)
        {
            lines = lines[..^1];
        }
        return (logFile.Name, lines);
    }
}
