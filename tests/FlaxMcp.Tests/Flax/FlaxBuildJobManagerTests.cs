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
    public async Task Start_WhenTheDelegateThrowsSynchronously_DoesNotThrowFromStartAndReportsAFailedJob()
    {
        // Start dispatches via Task.Run, which captures even a throw from before the delegate's first
        // await (e.g. the session lock already being held) into the returned Task's faulted state --
        // so a synchronous failure surfaces as a normal failed job instead of escaping this call.
        var manager = new FlaxBuildJobManager();

        var jobId = manager.Start(_ => throw new InvalidOperationException("synchronous failure"));
        await WaitForCompletionAsync(manager, jobId);

        Assert.Equal("failed", manager.GetStatus(jobId));
        var exception = Assert.Throws<InvalidOperationException>(() => manager.GetResult(jobId));
        Assert.Contains("synchronous failure", exception.Message);
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
