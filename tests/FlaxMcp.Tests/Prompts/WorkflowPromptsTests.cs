using FlaxMcp.Configuration;
using FlaxMcp.Prompts;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using Xunit;

namespace FlaxMcp.Tests.Prompts;

public sealed class WorkflowPromptsTests
{
    private static IReadOnlyList<McpServerPrompt> Register(FlaxMcpOptions options)
    {
        var services = new ServiceCollection();
        var builder = services.AddMcpServer();
        PromptRegistration.AddPrompts(builder, options);
        using var provider = services.BuildServiceProvider();
        return [.. provider.GetServices<McpServerPrompt>()];
    }

    [Fact]
    public void AddPrompts_WithDefaults_RegistersDiagnoseBuildFailure()
    {
        var prompt = Assert.Single(Register(new FlaxMcpOptions()));

        Assert.Equal("diagnose_build_failure", prompt.ProtocolPrompt.Name);
        Assert.Contains("build_compile_scripts", WorkflowPrompts.DiagnoseBuildFailure());
        Assert.Contains("logs_errors", WorkflowPrompts.DiagnoseBuildFailure());
    }

    [Theory]
    [InlineData("build")]
    [InlineData("logs")]
    [InlineData("project")]
    public void AddPrompts_WithoutBothRequiredToolsets_DoesNotRegister(string toolsets)
    {
        Assert.Empty(Register(new FlaxMcpOptions { Toolsets = toolsets }));
    }

    [Fact]
    public void AddPrompts_WithReadOnly_DoesNotRegister()
    {
        Assert.Empty(Register(new FlaxMcpOptions { ReadOnly = true }));
    }
}
