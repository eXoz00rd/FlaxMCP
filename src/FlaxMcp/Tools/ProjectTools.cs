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
}
