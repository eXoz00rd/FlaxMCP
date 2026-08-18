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
            return new FlaxProcessResult(process.ExitCode, TimedOut: false, await standardOutputTask, await standardErrorTask);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            KillIfRunning(process);
            return new FlaxProcessResult(ExitCode: null, TimedOut: true, await standardOutputTask, await standardErrorTask);
        }
        catch (OperationCanceledException)
        {
            KillIfRunning(process);
            throw;
        }
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
