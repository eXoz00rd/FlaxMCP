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
        Assert.Contains(tools, tool => tool.ProtocolTool.Name == "project_info");
    }

    [Fact]
    public void AddTools_WithReadOnly_OnlyRegistersReadOnlyTools()
    {
        // No write tool exists yet (v1 only has server_info/project_info), so this can't yet prove
        // exclusion — it guards against a future write tool leaking through the ReadOnly filter.
        var tools = Register(new FlaxMcpOptions { ReadOnly = true });

        Assert.NotEmpty(tools);
        Assert.All(tools, tool => Assert.True(tool.ProtocolTool.Annotations?.ReadOnlyHint));
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
