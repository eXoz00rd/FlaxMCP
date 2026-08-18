using System.Text;
using FlaxMcp.Flax;
using Xunit;

namespace FlaxMcp.Tests.Flax;

public sealed class FlaxLogReaderTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "FlaxMcpTests_" + Guid.NewGuid().ToString("N"));
    private readonly string _logsDir;

    public FlaxLogReaderTests()
    {
        _logsDir = Path.Combine(_tempDir, "Logs");
        Directory.CreateDirectory(_logsDir);
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void FindLatestLogFile_WithMultipleLogs_ReturnsMostRecentlyWritten()
    {
        var older = WriteLogFile("Log_1.txt", "old");
        var newer = WriteLogFile("Log_2.txt", "new");
        File.SetLastWriteTimeUtc(older, DateTime.UtcNow.AddMinutes(-10));
        File.SetLastWriteTimeUtc(newer, DateTime.UtcNow);

        var found = FlaxLogReader.FindLatestLogFile(_tempDir);

        Assert.NotNull(found);
        Assert.Equal(newer, found!.FullName);
    }

    [Fact]
    public void FindLatestLogFile_WithAfterUtcFilter_IgnoresOlderLogs()
    {
        var older = WriteLogFile("Log_1.txt", "old");
        File.SetLastWriteTimeUtc(older, DateTime.UtcNow.AddMinutes(-10));

        var found = FlaxLogReader.FindLatestLogFile(_tempDir, afterUtc: DateTime.UtcNow.AddMinutes(-1));

        Assert.Null(found);
    }

    [Fact]
    public void FindLatestLogFile_WithNoLogsDirectory_ReturnsNull()
    {
        var found = FlaxLogReader.FindLatestLogFile(Path.Combine(_tempDir, "Missing"));

        Assert.Null(found);
    }

    [Fact]
    public void ReadLog_WithUtf16LeByteOrderMark_DecodesWithoutMojibakeOrLeadingBomChar()
    {
        // Real FlaxEditor.exe logs start with a genuine 0xFF 0xFE UTF-16LE BOM, verified against a
        // live run despite being commonly described as BOM-less.
        var path = Path.Combine(_logsDir, "WithBom.txt");
        var bytes = new byte[] { 0xFF, 0xFE }.Concat(Encoding.Unicode.GetBytes("[Info] Compiled with no errors\n")).ToArray();
        File.WriteAllBytes(path, bytes);

        var text = FlaxLogReader.ReadLog(path);

        Assert.StartsWith("[Info]", text);
        Assert.DoesNotContain('\uFEFF', text);
    }

    [Fact]
    public void ReadLog_WithoutByteOrderMark_StillDecodesAsUtf16Le()
    {
        var path = Path.Combine(_logsDir, "NoBom.txt");
        File.WriteAllBytes(path, Encoding.Unicode.GetBytes("[Error] CS1069: broken reference\n"));

        var text = FlaxLogReader.ReadLog(path);

        Assert.Contains("CS1069", text);
    }

    [Fact]
    public void TryParseTotalErrors_WithZeroErrorsSummary_ReturnsZero()
    {
        var count = FlaxLogReader.TryParseTotalErrors("[Info] Build finished.\n Total errors: 0\n");

        Assert.Equal(0, count);
    }

    [Fact]
    public void TryParseTotalErrors_WithNonZeroErrorsSummary_ReturnsThatCount()
    {
        // FlaxEditor.exe's process exit code stays 0 even when this line reports failures -- this
        // summary is the only reliable signal, verified against a real broken-script headless run.
        var count = FlaxLogReader.TryParseTotalErrors("[Error] CS1519: ...\n Total errors: 3\n");

        Assert.Equal(3, count);
    }

    [Fact]
    public void TryParseTotalErrors_WithoutASummaryLine_ReturnsNull()
    {
        var count = FlaxLogReader.TryParseTotalErrors("[Info] Engine crashed before reaching the summary.\n");

        Assert.Null(count);
    }

    private string WriteLogFile(string name, string content)
    {
        var path = Path.Combine(_logsDir, name);
        File.WriteAllText(path, content);
        return path;
    }
}
