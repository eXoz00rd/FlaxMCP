using FlaxMcp.Configuration;
using Xunit;

namespace FlaxMcp.Tests.Configuration;

public sealed class ServerInstructionsTests
{
    [Fact]
    public void Build_MentionsProjectPathAndToolCount()
    {
        var instructions = ServerInstructions.Build(new FlaxMcpOptions { ProjectPath = @"D:\Projects\Mournfall" }, 5);

        Assert.Contains(@"D:\Projects\Mournfall", instructions);
        Assert.Contains("5 tools", instructions);
    }

    [Fact]
    public void Build_WithReadOnly_StatesTheRestriction()
    {
        var instructions = ServerInstructions.Build(new FlaxMcpOptions { ReadOnly = true }, 2);

        Assert.Contains("read-only mode", instructions);
    }

    [Fact]
    public void Build_WithoutReadOnly_OmitsTheRestriction()
    {
        var instructions = ServerInstructions.Build(new FlaxMcpOptions(), 2);

        Assert.DoesNotContain("read-only mode", instructions);
    }
}
