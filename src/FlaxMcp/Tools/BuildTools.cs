using System.ComponentModel;
using FlaxMcp.Configuration;
using FlaxMcp.Flax;
using FlaxMcp.Flax.Models;
using ModelContextProtocol.Server;

namespace FlaxMcp.Tools;

[McpServerToolType]
public sealed class BuildTools
{
    private static readonly TimeSpan ShortOperationTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan BuildTimeout = TimeSpan.FromMinutes(30);

    private readonly FlaxHeadlessEditorRunner _runner;
    private readonly FlaxBuildJobManager _jobs;

    public BuildTools(FlaxHeadlessEditorRunner runner, FlaxBuildJobManager jobs)
    {
        _runner = runner;
        _jobs = jobs;
    }

    [McpServerTool(Name = "build_generate_projects", ReadOnly = false, UseStructuredContent = true)]
    [Description("Runs FlaxEditor.exe -genprojectfiles to regenerate IDE project/solution files.")]
    public async Task<FlaxBuildOperationResult> GenerateProjects(CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(["-genprojectfiles"], ShortOperationTimeout, cancellationToken);
        return ToOperationResult(result);
    }

    [McpServerTool(Name = "build_compile_scripts", ReadOnly = false, UseStructuredContent = true)]
    [Description("Compiles game scripts headlessly. Returns structured compiler errors (file/line/code/message) parsed from the engine log, not a raw log dump.")]
    public async Task<FlaxBuildOperationResult> CompileScripts(CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync([], ShortOperationTimeout, cancellationToken);
        return ToOperationResult(result);
    }

    [McpServerTool(Name = "build_clear_cache", ReadOnly = false, UseStructuredContent = true)]
    [Description("Runs FlaxEditor.exe -clearcache, and optionally -clearcooker too, to clear build caches.")]
    public async Task<FlaxBuildOperationResult> ClearCache(bool alsoClearCooker, CancellationToken cancellationToken)
    {
        List<string> arguments = alsoClearCooker ? ["-clearcache", "-clearcooker"] : ["-clearcache"];
        var result = await _runner.RunAsync(arguments, ShortOperationTimeout, cancellationToken);
        return ToOperationResult(result);
    }

    [McpServerTool(Name = "build_game", ReadOnly = false, UseStructuredContent = true)]
    [Description("Starts a game build (-build <preset>.<target>, e.g. preset 'Development', target 'Windows') in the background and returns a job id immediately -- a real build can take minutes. Poll with build_status and fetch the outcome with build_result once it's done.")]
    public string StartBuild(string preset, string target)
    {
        return _jobs.Start(cancellationToken => _runner.RunAsync(["-build", $"{preset}.{target}"], BuildTimeout, cancellationToken));
    }

    [McpServerTool(Name = "build_status", ReadOnly = true, UseStructuredContent = true)]
    [Description("Reports whether a build_game job is 'running', 'completed', 'failed', or 'canceled'.")]
    public string GetBuildStatus(string jobId)
    {
        return _jobs.GetStatus(jobId);
    }

    [McpServerTool(Name = "build_result", ReadOnly = true, UseStructuredContent = true)]
    [Description("Fetches a build_game job's outcome once build_status reports it's no longer running. Throws if the job is still running, unknown, or ended abnormally.")]
    public FlaxBuildOperationResult GetBuildResult(string jobId)
    {
        return ToOperationResult(_jobs.GetResult(jobId));
    }

    internal static FlaxBuildOperationResult ToOperationResult(FlaxHeadlessRunResult result)
    {
        var errors = result.Log is not null ?
            FlaxCompilerDiagnosticParser.Parse(result.Log).Where(diagnostic => diagnostic.Severity == "error").Take(ResponseLimits.DefaultListTop).ToArray() :
            [];

        return new FlaxBuildOperationResult(
            Succeeded: result.ExitCode == 0 && !result.TimedOut && result.ErrorCount is null or 0,
            result.ExitCode,
            result.TimedOut,
            result.ErrorCount,
            errors
        );
    }
}
