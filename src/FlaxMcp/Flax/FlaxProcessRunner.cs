using System.Diagnostics;
using FlaxMcp.Flax.Models;

namespace FlaxMcp.Flax;

/// <summary>
/// Launches an external process (<c>FlaxEditor.exe</c>, <c>Flax.Build.exe</c>) with cancellation and
/// timeout support. A timeout kills the process and returns a result instead of throwing, so a
/// hung child never hangs the caller.
/// </summary>
public static class FlaxProcessRunner
{
    // Bounds how long RunAsync waits for the redirected pipes to reach EOF after the process exits
    // or is killed. FlaxEditor.exe's own build step spawns descendants (e.g. "dotnet exec csc.dll"),
    // and if one outlives entireProcessTree's kill and keeps a duplicated pipe handle open, plain
    // ReadToEndAsync would never see EOF -- this grace period keeps that from becoming an indefinite
    // hang, at the cost of possibly truncated output in that rare case.
    private static readonly TimeSpan DrainGracePeriod = TimeSpan.FromSeconds(5);

    public static async Task<FlaxProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        // Read the redirected streams directly rather than via OutputDataReceived/BeginOutputReadLine:
        // WaitForExitAsync completing doesn't guarantee those event callbacks have finished draining
        // buffered output, which can silently drop the tail of a fast-exiting process's output.
        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();

        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            await process.WaitForExitAsync(linkedCts.Token);
            var (standardOutput, standardError) = await DrainAsync(standardOutputTask, standardErrorTask);
            return new FlaxProcessResult(process.ExitCode, TimedOut: false, standardOutput, standardError);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            KillIfRunning(process);
            var (standardOutput, standardError) = await DrainAsync(standardOutputTask, standardErrorTask);
            return new FlaxProcessResult(ExitCode: null, TimedOut: true, standardOutput, standardError);
        }
        catch (OperationCanceledException)
        {
            KillIfRunning(process);
            throw;
        }
    }

    internal static async Task<(string StandardOutput, string StandardError)> DrainAsync(Task<string> standardOutputTask, Task<string> standardErrorTask)
    {
        var both = Task.WhenAll(standardOutputTask, standardErrorTask);
        if (await Task.WhenAny(both, Task.Delay(DrainGracePeriod)) != both)
        {
            return (
                standardOutputTask.IsCompletedSuccessfully ? standardOutputTask.Result : string.Empty,
                standardErrorTask.IsCompletedSuccessfully ? standardErrorTask.Result : string.Empty
            );
        }
        return (standardOutputTask.Result, standardErrorTask.Result);
    }

    private static void KillIfRunning(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Exited between the check and the kill.
        }
    }
}
