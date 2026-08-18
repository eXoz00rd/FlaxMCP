using FlaxMcp.Flax;
using FlaxMcp.Flax.Models;
using Xunit;

namespace FlaxMcp.Tests.Flax;

public sealed class FlaxBuildJobManagerTests
{
    [Fact]
    public void GetStatus_ForAStillRunningJob_ReturnsRunning()
    {
        var manager = new FlaxBuildJobManager();
        var completion = new TaskCompletionSource<FlaxHeadlessRunResult>();

        var jobId = manager.Start(_ => completion.Task);

        Assert.Equal("running", manager.GetStatus(jobId));
    }

    [Fact]
    public void GetResult_ForAStillRunningJob_Throws()
    {
        var manager = new FlaxBuildJobManager();
        var completion = new TaskCompletionSource<FlaxHeadlessRunResult>();
        var jobId = manager.Start(_ => completion.Task);

        Assert.Throws<InvalidOperationException>(() => manager.GetResult(jobId));
    }

    [Fact]
    public async Task GetStatusAndResult_AfterCompletion_ReportCompletedAndReturnTheResult()
    {
        var manager = new FlaxBuildJobManager();
        var completion = new TaskCompletionSource<FlaxHeadlessRunResult>();
        var jobId = manager.Start(_ => completion.Task);
        var expected = new FlaxHeadlessRunResult(0, TimedOut: false, Log: "done", ErrorCount: 0);

        completion.SetResult(expected);
        await completion.Task;

        Assert.Equal("completed", manager.GetStatus(jobId));
        Assert.Same(expected, manager.GetResult(jobId));
    }

    [Fact]
    public async Task GetStatusAndResult_AfterFailure_ReportFailedAndThrowWithTheUnderlyingMessage()
    {
        var manager = new FlaxBuildJobManager();
        var completion = new TaskCompletionSource<FlaxHeadlessRunResult>();
        var jobId = manager.Start(_ => completion.Task);

        completion.SetException(new InvalidOperationException("boom"));
        await Assert.ThrowsAnyAsync<Exception>(() => completion.Task);

        Assert.Equal("failed", manager.GetStatus(jobId));
        var exception = Assert.Throws<InvalidOperationException>(() => manager.GetResult(jobId));
        Assert.Contains("boom", exception.Message);
    }

    [Fact]
    public void GetStatus_WithUnknownJobId_Throws()
    {
        var manager = new FlaxBuildJobManager();

        Assert.Throws<InvalidOperationException>(() => manager.GetStatus("does-not-exist"));
    }

    [Fact]
    public void GetResult_WithUnknownJobId_Throws()
    {
        var manager = new FlaxBuildJobManager();

        Assert.Throws<InvalidOperationException>(() => manager.GetResult("does-not-exist"));
    }
}
