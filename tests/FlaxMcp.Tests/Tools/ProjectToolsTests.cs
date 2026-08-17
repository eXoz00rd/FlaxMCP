using FlaxMcp.Configuration;
using FlaxMcp.Tools;
using Microsoft.Extensions.Options;
using Xunit;

namespace FlaxMcp.Tests.Tools;

public sealed class ProjectToolsTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "FlaxMcpTests_" + Guid.NewGuid().ToString("N"));

    public ProjectToolsTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void GetProjectInfo_ResolvesAndReadsTheProjectFile()
    {
        File.WriteAllText(Path.Combine(_tempDir, "Game.flaxproj"), """{ "Name": "Game", "Version": "2.0" }""");
        var tool = new ProjectTools(Options.Create(new FlaxMcpOptions { ProjectPath = _tempDir }));

        var info = tool.GetProjectInfo();

        Assert.Equal("Game", info.Name);
        Assert.Equal("2.0", info.Version);
    }
}
