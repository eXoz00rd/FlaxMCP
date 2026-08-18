using System.Collections.Concurrent;
using FlaxMcp.Flax.Models;

namespace FlaxMcp.Flax;

/// <summary>
/// Tracks background <c>build_game</c> runs so the MCP call that starts one can return a job id
/// immediately instead of blocking for the minutes a real engine build can take (plan risk R5).
/// Jobs are kept in memory only, for the lifetime of the server process -- acceptable since a lost
/// job's underlying <c>FlaxEditor.exe</c> process is unaffected and the build can simply be restarted.
/// </summary>
public sealed class FlaxBuildJobManager
{
    private readonly ConcurrentDictionary<string, Task<FlaxHeadlessRunResult>> _jobs = new();

    /// <summary>
    /// Starts <paramref name="run"/> in the background and returns its job id immediately. The job is
    /// not tied to the calling MCP request's cancellation -- it keeps running after that call returns.
    /// </summary>
    public string Start(Func<CancellationToken, Task<FlaxHeadlessRunResult>> run)
    {
        var jobId = Guid.NewGuid().ToString("N");
        _jobs[jobId] = run(CancellationToken.None);
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
    public FlaxHeadlessRunResult GetResult(string jobId)
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

    private Task<FlaxHeadlessRunResult> GetJob(string jobId)
    {
        return _jobs.TryGetValue(jobId, out var job) ? job : throw new InvalidOperationException($"Unknown build job id '{jobId}'.");
    }
}
