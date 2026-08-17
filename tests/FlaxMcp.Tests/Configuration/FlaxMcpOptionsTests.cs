using FlaxMcp.Configuration;
using Xunit;

namespace FlaxMcp.Tests.Configuration;

public sealed class FlaxMcpOptionsTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "FlaxMcpTests_" + Guid.NewGuid().ToString("N"));

    public FlaxMcpOptionsTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void ResolveProjectFile_WithMissingProjectPath_Throws()
    {
        var options = new FlaxMcpOptions { ProjectPath = string.Empty };

        var exception = Assert.Throws<InvalidOperationException>(() => options.ResolveProjectFile());

        Assert.Contains(FlaxMcpOptions.ProjectPathVariable, exception.Message);
    }

    [Fact]
    public void ResolveProjectFile_PointingDirectlyAtFile_ReturnsIt()
    {
        var projectFile = Path.Combine(_tempDir, "Game.flaxproj");
        File.WriteAllText(projectFile, "{}");
        var options = new FlaxMcpOptions { ProjectPath = projectFile };

        var resolved = options.ResolveProjectFile();

        Assert.Equal(Path.GetFullPath(projectFile), resolved);
    }

    [Fact]
    public void ResolveProjectFile_PointingAtMissingFile_Throws()
    {
        var options = new FlaxMcpOptions { ProjectPath = Path.Combine(_tempDir, "Missing.flaxproj") };

        Assert.Throws<InvalidOperationException>(() => options.ResolveProjectFile());
    }

    [Fact]
    public void ResolveProjectFile_PointingAtDirectoryWithOneProject_ReturnsIt()
    {
        var projectFile = Path.Combine(_tempDir, "Game.flaxproj");
        File.WriteAllText(projectFile, "{}");
        var options = new FlaxMcpOptions { ProjectPath = _tempDir };

        var resolved = options.ResolveProjectFile();

        Assert.Equal(Path.GetFullPath(projectFile), resolved);
    }

    [Fact]
    public void ResolveProjectFile_PointingAtDirectoryWithNoProject_Throws()
    {
        var options = new FlaxMcpOptions { ProjectPath = _tempDir };

        var exception = Assert.Throws<InvalidOperationException>(() => options.ResolveProjectFile());

        Assert.Contains(_tempDir, exception.Message);
    }

    [Fact]
    public void ResolveProjectFile_PointingAtDirectoryWithMultipleProjects_ThrowsListingThem()
    {
        File.WriteAllText(Path.Combine(_tempDir, "One.flaxproj"), "{}");
        File.WriteAllText(Path.Combine(_tempDir, "Two.flaxproj"), "{}");
        var options = new FlaxMcpOptions { ProjectPath = _tempDir };

        var exception = Assert.Throws<InvalidOperationException>(() => options.ResolveProjectFile());

        Assert.Contains("One.flaxproj", exception.Message);
        Assert.Contains("Two.flaxproj", exception.Message);
    }

    [Fact]
    public void ResolveProjectFile_PointingAtMissingDirectory_Throws()
    {
        var options = new FlaxMcpOptions { ProjectPath = Path.Combine(_tempDir, "DoesNotExist") };

        Assert.Throws<InvalidOperationException>(() => options.ResolveProjectFile());
    }
}
