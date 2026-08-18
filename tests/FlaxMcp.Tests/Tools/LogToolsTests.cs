using System.Text;
using FlaxMcp.Configuration;
using FlaxMcp.Tools;
using Microsoft.Extensions.Options;
using Xunit;

namespace FlaxMcp.Tests.Tools;

public sealed class LogToolsTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "FlaxMcpTests_" + Guid.NewGuid().ToString("N"));
    private readonly string _logsDir;
    private readonly LogTools _tool;

    public LogToolsTests()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(Path.Combine(_tempDir, "Game.flaxproj"), """{ "Name": "Game" }""");
        _logsDir = Path.Combine(_tempDir, "Logs");
        Directory.CreateDirectory(_logsDir);

        var options = Options.Create(new FlaxMcpOptions { ProjectPath = _tempDir });
        _tool = new LogTools(options);
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void GetLogsTail_ReturnsLastLinesFromTheMostRecentLogFile()
    {
        WriteLogFile("Log_1.txt", "line1\nline2\nline3\n", DateTime.UtcNow.AddMinutes(-10));
        WriteLogFile("Log_2.txt", "a\nb\nc\nd\n", DateTime.UtcNow);

        var tail = _tool.GetLogsTail(lines: 2);

        Assert.Equal("Log_2.txt", tail.FileName);
        Assert.Equal(["c", "d"], tail.Lines);
    }

    [Fact]
    public void GetLogsTail_ClampsAnOversizedLinesRequest()
    {
        var totalLines = ResponseLimits.DefaultMaxItems + 100;
        WriteLogFile("Log_1.txt", string.Concat(Enumerable.Range(0, totalLines).Select(i => $"line{i}\n")), DateTime.UtcNow);

        var tail = _tool.GetLogsTail(lines: 1_000_000);

        Assert.Equal(ResponseLimits.DefaultMaxItems, tail.Lines.Count);
        Assert.Equal($"line{totalLines - 1}", tail.Lines[^1]);
    }

    [Fact]
    public void GetLogsTail_WithNoLogFiles_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => _tool.GetLogsTail());
    }

    [Fact]
    public void GetLogErrors_ExtractsOnlyErrorAndWarningTaggedLines()
    {
        WriteLogFile(
            "Log_1.txt",
            "[Info] Starting up\n[Error] Task failed with exit code 1\n[Warning] Deprecated API used\n[Info] Done\n",
            DateTime.UtcNow
        );

        var errors = _tool.GetLogErrors();

        Assert.Equal(["[Error] Task failed with exit code 1", "[Warning] Deprecated API used"], errors);
    }

    private void WriteLogFile(string name, string content, DateTime lastWriteUtc)
    {
        var path = Path.Combine(_logsDir, name);
        File.WriteAllBytes(path, Encoding.Unicode.GetBytes(content));
        File.SetLastWriteTimeUtc(path, lastWriteUtc);
    }
}
