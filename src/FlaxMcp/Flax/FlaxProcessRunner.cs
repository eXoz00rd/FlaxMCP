using System.Diagnostics;
using System.Text;
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
        var standardOutput = new StringBuilder();
        var standardError = new StringBuilder();
        process.OutputDataReceived += (_, args) => { if (args.Data is not null) { standardOutput.AppendLine(args.Data); } };
        process.ErrorDataReceived += (_, args) => { if (args.Data is not null) { standardError.AppendLine(args.Data); } };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            await process.WaitForExitAsync(linkedCts.Token);
            return new FlaxProcessResult(process.ExitCode, TimedOut: false, standardOutput.ToString(), standardError.ToString());
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            KillIfRunning(process);
            return new FlaxProcessResult(ExitCode: null, TimedOut: true, standardOutput.ToString(), standardError.ToString());
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
