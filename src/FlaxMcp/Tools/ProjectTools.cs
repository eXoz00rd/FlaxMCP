using System.ComponentModel;
using FlaxMcp.Configuration;
using FlaxMcp.Flax;
using FlaxMcp.Flax.Models;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace FlaxMcp.Tools;

[McpServerToolType]
public sealed class ProjectTools
{
    private readonly IOptions<FlaxMcpOptions> _options;

    public ProjectTools(IOptions<FlaxMcpOptions> options)
    {
        _options = options;
    }

    [McpServerTool(Name = "project_info", ReadOnly = true, UseStructuredContent = true)]
    [Description("Reads the .flaxproj file: name, version, targets, references, and default scene.")]
    public FlaxProjectInfo GetProjectInfo()
    {
        var projectFile = _options.Value.ResolveProjectFile();
        return FlaxProjectReader.Read(projectFile);
    }

    [McpServerTool(Name = "project_targets", ReadOnly = true, UseStructuredContent = true)]
    [Description("Parses the GameTarget/EditorTarget *.Build.cs files referenced from the .flaxproj: target class names and referenced modules.")]
    public FlaxProjectTargetsInfo GetProjectTargets()
    {
        var projectFile = _options.Value.ResolveProjectFile();
        var projectInfo = FlaxProjectReader.Read(projectFile);
        var sourceDirectory = Path.Combine(Path.GetDirectoryName(projectFile)!, "Source");

        return new FlaxProjectTargetsInfo(
            GameTarget: FlaxBuildTargetReader.Read(Path.Combine(sourceDirectory, projectInfo.GameTarget + ".Build.cs")),
            EditorTarget: FlaxBuildTargetReader.Read(Path.Combine(sourceDirectory, projectInfo.EditorTarget + ".Build.cs"))
        );
    }

    [McpServerTool(Name = "project_settings", ReadOnly = true, UseStructuredContent = true)]
    [Description("Reads Content/*.json and Content/Settings/*.json settings assets (Game, Graphics, Input, Physics, ...) as structured JSON.")]
    public IReadOnlyList<FlaxSettingsFile> GetProjectSettings()
    {
        var projectFile = _options.Value.ResolveProjectFile();
        var projectDirectory = Path.GetDirectoryName(projectFile)!;
        return FlaxProjectSettingsReader.ReadAll(projectDirectory);
    }
}
