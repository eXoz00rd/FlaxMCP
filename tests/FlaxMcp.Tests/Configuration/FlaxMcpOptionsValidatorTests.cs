using FlaxMcp.Configuration;
using Xunit;

namespace FlaxMcp.Tests.Configuration;

public sealed class FlaxMcpOptionsValidatorTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "FlaxMcpTests_" + Guid.NewGuid().ToString("N"));
    private readonly string _projectDir;
    private readonly string _engineDir;
    private readonly FlaxMcpOptionsValidator _validator = new();

    public FlaxMcpOptionsValidatorTests()
    {
        _projectDir = Path.Combine(_tempDir, "Project");
        _engineDir = Path.Combine(_tempDir, "Engine");
        Directory.CreateDirectory(_projectDir);
        File.WriteAllText(Path.Combine(_projectDir, "Game.flaxproj"), "{}");

        Directory.CreateDirectory(_engineDir);
        File.WriteAllText(Path.Combine(_engineDir, "Flax.flaxproj"), "{}");
        var editorDir = Path.Combine(_engineDir, "Binaries", "Editor", "Win64", "Development");
        Directory.CreateDirectory(editorDir);
        File.WriteAllText(Path.Combine(editorDir, "FlaxEditor.exe"), string.Empty);
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
    }

    private FlaxMcpOptions CreateValidOptions()
    {
        return new FlaxMcpOptions { ProjectPath = _projectDir, EnginePath = _engineDir };
    }

    [Fact]
    public void Validate_WithValidOptions_Succeeds()
    {
        var result = _validator.Validate(null, CreateValidOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WithMissingProjectPath_Fails()
    {
        var options = CreateValidOptions();
        options.ProjectPath = string.Empty;

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(FlaxMcpOptions.ProjectPathVariable, result.FailureMessage);
    }

    [Fact]
    public void Validate_WithInvalidEnginePath_Fails()
    {
        var options = CreateValidOptions();
        options.EnginePath = Path.Combine(_tempDir, "NotAnEngine");

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(FlaxMcpOptions.EnginePathVariable, result.FailureMessage);
    }

    [Fact]
    public void Validate_WithMissingEditorExecutable_Fails()
    {
        var options = CreateValidOptions();
        options.EditorConfig = "Release";

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("FlaxEditor.exe", result.FailureMessage);
        Assert.Contains("Release", result.FailureMessage);
    }
}
