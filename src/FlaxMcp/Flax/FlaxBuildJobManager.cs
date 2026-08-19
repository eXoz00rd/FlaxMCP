using System.Collections.Concurrent;
using FlaxMcp.Flax.Models;

namespace FlaxMcp.Flax;

/// <summary>
/// Tracks background <c>build_game</c> runs so the MCP call that starts one can return a job id
/// immediately instead of blocking for the minutes a real engine build can take (plan risk R5). Jobs
/// store the already-mapped <see cref="FlaxBuildOperationResult"/> rather than the raw
/// <see cref="FlaxHeadlessRunResult"/>, so a completed job doesn't pin the full engine log text in
/// memory for the rest of the server's lifetime. Jobs are kept in memory only, for the lifetime of the
/// server process -- acceptable since a lost job's underlying <c>FlaxEditor.exe</c> process is
/// unaffected and the build can simply be restarted.
/// </summary>
public sealed class FlaxBuildJobManager
{
    private readonly ConcurrentDictionary<string, Task<FlaxBuildOperationResult>> _jobs = new();

    /// <summary>
    /// Starts <paramref name="run"/> in the background and returns its job id immediately. Dispatched
    /// via <see cref="Task.Run(Func{Task})"/> so that even the synchronous prefix of <paramref name="run"/>
    /// (resolving paths, acquiring the session lock, <c>Process.Start</c>) executes off the calling
    /// thread -- otherwise the caller would block on that prefix, and an exception from it (e.g. the
    /// session lock already being held) would propagate out of this call instead of becoming a normal
    /// "failed" job. The job is not tied to the calling MCP request's cancellation -- it keeps running
    /// after that call returns.
    /// </summary>
    public string Start(Func<CancellationToken, Task<FlaxBuildOperationResult>> run)
    {
        var jobId = Guid.NewGuid().ToString("N");
        _jobs[jobId] = Task.Run(() => run(CancellationToken.None));
        return jobId;
    }

    public string GetStatus(string jobId)
    {
        return GetJob(jobId).Status switch
        {
            TaskStatus.RanToCompletion => "completed",
            TaskStatus.Faulted => "failed",
            TaskStatus.Canceled => "canceled",
            _ => "running",
        };
    }

    /// <summary>
    /// Returns the completed job's result, or throws if it's still running or ended abnormally.
    /// </summary>
    public FlaxBuildOperationResult GetResult(string jobId)
    {
        var job = GetJob(jobId);

        if (!job.IsCompleted)
        {
            throw new InvalidOperationException($"Build job '{jobId}' is still running. Check build_status first.");
        }
        if (job.IsFaulted)
        {
            var error = job.Exception!.InnerException ?? job.Exception!;
            throw new InvalidOperationException($"Build job '{jobId}' failed: {error.Message}");
        }
        if (job.IsCanceled)
        {
            throw new InvalidOperationException($"Build job '{jobId}' was canceled.");
        }

        return job.Result;
    }

    private Task<FlaxBuildOperationResult> GetJob(string jobId)
    {
        return _jobs.TryGetValue(jobId, out var job) ? job : throw new InvalidOperationException($"Unknown build job id '{jobId}'.");
    }
}
