using FlaxMcp.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using Xunit;

namespace FlaxMcp.Tests.Configuration;

public sealed class ToolRegistrationTests
{
    private static IReadOnlyList<McpServerTool> Register(FlaxMcpOptions options)
    {
        var services = new ServiceCollection();
        ToolRegistration.AddTools(services, options);
        using var provider = services.BuildServiceProvider();
        return [.. provider.GetServices<McpServerTool>()];
    }

    [Fact]
    public void AddTools_WithDefaults_RegistersEveryTool()
    {
        var tools = Register(new FlaxMcpOptions());

        Assert.Contains(tools, tool => tool.ProtocolTool.Name == "server_info");
        Assert.Contains(tools, tool => tool.ProtocolTool.Name == "flax_status");
        Assert.Contains(tools, tool => tool.ProtocolTool.Name == "project_info");
        Assert.Contains(tools, tool => tool.ProtocolTool.Name == "editor_scene_graph");
        Assert.Contains(tools, tool => tool.ProtocolTool.Name == "editor_get_selection");
        Assert.Contains(tools, tool => tool.ProtocolTool.Name == "editor_set_selection");
        Assert.Contains(tools, tool => tool.ProtocolTool.Name == "editor_actor_details");
        Assert.Contains(tools, tool => tool.ProtocolTool.Name == "editor_modify_actor");
        Assert.Contains(tools, tool => tool.ProtocolTool.Name == "editor_save");
    }

    [Fact]
    public void AddTools_WithReadOnly_OnlyRegistersReadOnlyTools()
    {
        var tools = Register(new FlaxMcpOptions { ReadOnly = true });

        Assert.NotEmpty(tools);
        Assert.All(tools, tool => Assert.True(tool.ProtocolTool.Annotations?.ReadOnlyHint));
    }

    [Fact]
    public void AddTools_WithReadOnly_ExcludesWriteToolsButKeepsTheirReadOnlySiblings()
    {
        var names = Register(new FlaxMcpOptions { ReadOnly = true }).Select(tool => tool.ProtocolTool.Name).ToList();

        Assert.DoesNotContain("build_generate_projects", names);
        Assert.DoesNotContain("build_compile_scripts", names);
        Assert.DoesNotContain("build_clear_cache", names);
        Assert.DoesNotContain("build_game", names);
        Assert.Contains("build_status", names);
        Assert.Contains("build_result", names);
        Assert.Contains("logs_tail", names);
        Assert.Contains("logs_errors", names);
        Assert.Contains("editor_get_selection", names);
        Assert.Contains("editor_actor_details", names);
        Assert.DoesNotContain("editor_set_selection", names);
        Assert.DoesNotContain("editor_modify_actor", names);
        Assert.DoesNotContain("editor_save", names);
    }

    [Fact]
    public void AddTools_WithToolsetSelection_AlwaysKeepsServerToolset()
    {
        var tools = Register(new FlaxMcpOptions { Toolsets = "project" });

        var names = tools.Select(tool => tool.ProtocolTool.Name).ToList();
        Assert.Contains("server_info", names);
        Assert.Contains("project_info", names);
    }

    [Fact]
    public void AddTools_PublishesOutputSchemasForStructuredResults()
    {
        var tools = Register(new FlaxMcpOptions());

        var projectInfo = Assert.Single(tools, tool => tool.ProtocolTool.Name == "project_info");
        Assert.NotNull(projectInfo.ProtocolTool.OutputSchema);
    }
}
