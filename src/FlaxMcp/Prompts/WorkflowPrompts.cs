using System.ComponentModel;
using ModelContextProtocol.Server;

namespace FlaxMcp.Prompts;

[McpServerPromptType]
public static class WorkflowPrompts
{
    [McpServerPrompt(Name = "diagnose_build_failure")]
    [Description("Compiles Flax game scripts and diagnoses the failure using structured diagnostics and the engine log.")]
    public static string DiagnoseBuildFailure()
    {
        return """
               Diagnose the Flax project's script build failure.

               Steps:
               1. Call build_compile_scripts and inspect its structured compiler errors.
               2. Call logs_errors to find engine errors or warnings that explain failures not represented by compiler diagnostics.
               3. Correlate both results by file, timestamp, and message. Prefer the earliest actionable error over cascading failures.

               Then report: whether compilation succeeded, the root cause, the affected file and line when available,
               and the smallest concrete fix. Separate compiler diagnostics from engine or project-configuration errors.
               Do not modify files or clear caches unless I ask.
               """;
    }
}
