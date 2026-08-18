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
        var completion = new TaskCompletionSource<FlaxBuildOperationResult>();

        var jobId = manager.Start(_ => completion.Task);

        Assert.Equal("running", manager.GetStatus(jobId));
    }

    [Fact]
    public void GetResult_ForAStillRunningJob_Throws()
    {
        var manager = new FlaxBuildJobManager();
        var completion = new TaskCompletionSource<FlaxBuildOperationResult>();
        var jobId = manager.Start(_ => completion.Task);

        Assert.Throws<InvalidOperationException>(() => manager.GetResult(jobId));
    }

    [Fact]
    public async Task GetStatusAndResult_AfterCompletion_ReportCompletedAndReturnTheResult()
    {
        var manager = new FlaxBuildJobManager();
        var completion = new TaskCompletionSource<FlaxBuildOperationResult>();
        var jobId = manager.Start(_ => completion.Task);
        var expected = new FlaxBuildOperationResult(Succeeded: true, ExitCode: 0, TimedOut: false, ErrorCount: 0, Errors: []);

        completion.SetResult(expected);
        await completion.Task;
        await WaitForCompletionAsync(manager, jobId);

        Assert.Equal("completed", manager.GetStatus(jobId));
        Assert.Same(expected, manager.GetResult(jobId));
    }

    [Fact]
    public async Task GetStatusAndResult_AfterFailure_ReportFailedAndThrowWithTheUnderlyingMessage()
    {
        var manager = new FlaxBuildJobManager();
        var completion = new TaskCompletionSource<FlaxBuildOperationResult>();
        var jobId = manager.Start(_ => completion.Task);

        completion.SetException(new InvalidOperationException("boom"));
        await Assert.ThrowsAnyAsync<Exception>(() => completion.Task);
        await WaitForCompletionAsync(manager, jobId);

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

    [Fact]
    public async Task Start_DoesNotRunTheDelegateOnTheCallingThread()
    {
        // Start dispatches via Task.Run so that even a synchronous prefix inside the delegate (path
        // resolution, session-lock acquisition, Process.Start) never blocks the caller.
        var manager = new FlaxBuildJobManager();
        var callingThreadId = Environment.CurrentManagedThreadId;
        var observedThreadId = -1;

        var jobId = manager.Start(_ =>
        {
            observedThreadId = Environment.CurrentManagedThreadId;
            return Task.FromResult(new FlaxBuildOperationResult(true, 0, false, 0, []));
        });

        await WaitForCompletionAsync(manager, jobId);
        Assert.NotEqual(callingThreadId, observedThreadId);
    }

    private static async Task WaitForCompletionAsync(FlaxBuildJobManager manager, string jobId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (manager.GetStatus(jobId) == "running" && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
    }
}
