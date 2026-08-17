using FlaxMcp.Flax;
using Xunit;

namespace FlaxMcp.Tests.Flax;

public sealed class EngineLocatorTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "FlaxMcpTests_" + Guid.NewGuid().ToString("N"));

    public EngineLocatorTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void ParseVersionsFile_WithSinglePair_ReturnsThatPath()
    {
        var paths = EngineLocator.ParseVersionsFile(["0", @"D:\Gry\Flax"]);

        Assert.Equal([@"D:\Gry\Flax"], paths);
    }

    [Fact]
    public void ParseVersionsFile_WithMultiplePairs_ReturnsEachPath()
    {
        var paths = EngineLocator.ParseVersionsFile(["0", @"D:\Gry\Flax", "1", @"D:\Gry\Flax2"]);

        Assert.Equal([@"D:\Gry\Flax", @"D:\Gry\Flax2"], paths);
    }

    [Fact]
    public void ParseVersionsFile_WithNoLines_ReturnsEmpty()
    {
        var paths = EngineLocator.ParseVersionsFile([]);

        Assert.Empty(paths);
    }

    [Fact]
    public void Resolve_WithValidExplicitPath_ReturnsIt()
    {
        File.WriteAllText(Path.Combine(_tempDir, "Flax.flaxproj"), "{}");

        var resolved = EngineLocator.Resolve(_tempDir);

        Assert.Equal(_tempDir, resolved);
    }

    [Fact]
    public void Resolve_WithExplicitPathMissingFlaxproj_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => EngineLocator.Resolve(_tempDir));

        Assert.Contains(_tempDir, exception.Message);
    }
}
