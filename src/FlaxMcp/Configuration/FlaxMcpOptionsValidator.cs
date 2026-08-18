using FlaxMcp.Flax;
using Microsoft.Extensions.Options;

namespace FlaxMcp.Configuration;

public sealed class FlaxMcpOptionsValidator : IValidateOptions<FlaxMcpOptions>
{
    public ValidateOptionsResult Validate(string? name, FlaxMcpOptions options)
    {
        try
        {
            options.ResolveProjectFile();
        }
        catch (InvalidOperationException ex)
        {
            return ValidateOptionsResult.Fail(ex.Message);
        }

        string enginePath;
        try
        {
            enginePath = EngineLocator.Resolve(options.EnginePath);
        }
        catch (InvalidOperationException ex)
        {
            return ValidateOptionsResult.Fail(ex.Message);
        }

        var editorExe = Path.Combine(enginePath, "Binaries", "Editor", "Win64", options.EditorConfig, "FlaxEditor.exe");
        if (!File.Exists(editorExe))
        {
            return ValidateOptionsResult.Fail(
                $"FlaxEditor.exe not found at '{editorExe}'. Check {FlaxMcpOptions.EnginePathVariable} and " +
                $"{FlaxMcpOptions.EditorConfigVariable} ('{options.EditorConfig}')."
            );
        }

        return ValidateOptionsResult.Success;
    }
}
