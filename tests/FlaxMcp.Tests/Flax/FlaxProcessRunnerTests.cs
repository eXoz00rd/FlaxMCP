using FlaxMcp.Flax;
using Xunit;

namespace FlaxMcp.Tests.Flax;

public sealed class FlaxProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_WithSuccessfulProcess_ReturnsExitCodeAndOutput()
    {
        var (fileName, arguments) = ShellCommand("echo hello-flax-mcp");

        var result = await FlaxProcessRunner.RunAsync(fileName, arguments, Path.GetTempPath(), TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.False(result.TimedOut);
        Assert.Contains("hello-flax-mcp", result.StandardOutput);
    }

    [Fact]
    public async Task RunAsync_WithNonZeroExit_ReturnsThatExitCode()
    {
        var (fileName, arguments) = ShellCommand("exit 3");

        var result = await FlaxProcessRunner.RunAsync(fileName, arguments, Path.GetTempPath(), TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal(3, result.ExitCode);
        Assert.False(result.TimedOut);
    }

    [Fact]
    public async Task RunAsync_ExceedingTimeout_KillsProcessAndReportsTimedOut()
    {
        var (fileName, arguments) = SleepCommand(TimeSpan.FromSeconds(30));

        var result = await FlaxProcessRunner.RunAsync(fileName, arguments, Path.GetTempPath(), TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);

        Assert.Null(result.ExitCode);
        Assert.True(result.TimedOut);
    }

    [Fact]
    public async Task RunAsync_WithCancelledToken_ThrowsAndKillsProcess()
    {
        var (fileName, arguments) = SleepCommand(TimeSpan.FromSeconds(30));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => FlaxProcessRunner.RunAsync(fileName, arguments, Path.GetTempPath(), TimeSpan.FromSeconds(10), cts.Token)
        );
    }

    [Fact]
    public async Task DrainAsync_WhenAStreamNeverReachesEof_ReturnsWithinTheGracePeriodInstead()
    {
        // Simulates a descendant process outliving Process.Kill and keeping a duplicated pipe handle
        // open (FlaxEditor.exe's build step spawns "dotnet exec csc.dll" as a child) -- ReadToEndAsync
        // on that stream would otherwise never see EOF and hang indefinitely.
        var stuckStandardOutput = new TaskCompletionSource<string>().Task;
        var completedStandardError = Task.FromResult("captured before the hang");

        var (standardOutput, standardError) = await FlaxProcessRunner.DrainAsync(stuckStandardOutput, completedStandardError);

        Assert.Equal(string.Empty, standardOutput);
        Assert.Equal("captured before the hang", standardError);
    }

    [Fact]
    public async Task DrainAsync_WhenBothStreamsCompletePromptly_ReturnsTheirContent()
    {
        var (standardOutput, standardError) = await FlaxProcessRunner.DrainAsync(Task.FromResult("out"), Task.FromResult("err"));

        Assert.Equal("out", standardOutput);
        Assert.Equal("err", standardError);
    }

    private static (string FileName, string[] Arguments) ShellCommand(string command)
    {
        return OperatingSystem.IsWindows() ? ("cmd.exe", ["/c", command]) : ("/bin/sh", ["-c", command]);
    }

    private static (string FileName, string[] Arguments) SleepCommand(TimeSpan duration)
    {
        return OperatingSystem.IsWindows() ?
            ("cmd.exe", ["/c", $"ping -n {(int)duration.TotalSeconds + 1} 127.0.0.1 >NUL"]) :
            ("/bin/sh", ["-c", $"sleep {(int)duration.TotalSeconds}"]);
    }
}
