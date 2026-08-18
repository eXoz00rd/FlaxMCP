using FlaxMcp.Flax.Models;
using FlaxMcp.Tools;
using Xunit;

namespace FlaxMcp.Tests.Tools;

public sealed class BuildToolsTests
{
    [Fact]
    public void ToOperationResult_WithCleanRun_ReportsSucceeded()
    {
        var result = BuildTools.ToOperationResult(new FlaxHeadlessRunResult(0, TimedOut: false, Log: "[Info] Compiled with no errors\n Total errors: 0\n", ErrorCount: 0));

        Assert.True(result.Succeeded);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ToOperationResult_WithZeroExitCodeButNonZeroErrorCount_ReportsNotSucceeded()
    {
        // FlaxEditor.exe's process exit code stays 0 even when script compilation fails -- ErrorCount,
        // not ExitCode, is the real success/failure signal (see FlaxHeadlessEditorRunner).
        var log = @"D:\Game\Broken.cs(1,1,1,2): error CS1519: Invalid token" + "\n Total errors: 1\n";
        var result = BuildTools.ToOperationResult(new FlaxHeadlessRunResult(0, TimedOut: false, log, ErrorCount: 1));

        Assert.False(result.Succeeded);
        var error = Assert.Single(result.Errors);
        Assert.Equal("CS1519", error.Code);
    }

    [Fact]
    public void ToOperationResult_WithTimeout_ReportsNotSucceeded()
    {
        var result = BuildTools.ToOperationResult(new FlaxHeadlessRunResult(null, TimedOut: true, Log: null, ErrorCount: null));

        Assert.False(result.Succeeded);
        Assert.True(result.TimedOut);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ToOperationResult_FiltersOutWarnings()
    {
        var log = @"D:\Game\Foo.cs(1,1): warning CS0168: unused variable" + "\n Total errors: 0\n";
        var result = BuildTools.ToOperationResult(new FlaxHeadlessRunResult(0, TimedOut: false, log, ErrorCount: 0));

        Assert.Empty(result.Errors);
    }
}
