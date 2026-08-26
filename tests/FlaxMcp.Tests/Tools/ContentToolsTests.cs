using FlaxMcp.Configuration;
using FlaxMcp.Flax;
using FlaxMcp.Tools;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using Xunit;

namespace FlaxMcp.Tests.Tools;

public sealed class ContentToolsTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "FlaxMcpTests_" + Guid.NewGuid().ToString("N"));
    private readonly ContentTools _tool;

    public ContentToolsTests()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(Path.Combine(_tempDir, "Game.flaxproj"), """{ "Name": "Game" }""");
        var contentDir = Path.Combine(_tempDir, "Content");
        Directory.CreateDirectory(contentDir);
        File.WriteAllText(
            Path.Combine(contentDir, "GameSettings.json"),
            """{ "ID": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "TypeName": "FlaxEditor.Content.Settings.GameSettings", "Data": {} }"""
        );

        var index = new FlaxContentIndex(Options.Create(new FlaxMcpOptions { ProjectPath = _tempDir }));
        _tool = new ContentTools(index, new FlaxBridgeClient(_tempDir, _tempDir));
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void SearchContent_FindsIndexedAsset()
    {
        var results = _tool.SearchContent(query: "GameSettings");

        var match = Assert.Single(results);
        Assert.Equal("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", match.Id);
    }

    [Fact]
    public void GetAssetInfo_ResolvesKnownId()
    {
        var info = _tool.GetAssetInfo("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

        Assert.Equal("GameSettings.json", info.RelativePath);
    }

    [Fact]
    public void GetAssetInfo_WithUnknownId_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => _tool.GetAssetInfo("does-not-exist"));
    }

    [Fact]
    public void ResolveGuid_ReturnsRelativePath()
    {
        var path = _tool.ResolveGuid("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

        Assert.Equal("GameSettings.json", path);
    }

    [Fact]
    public void ResolveGuid_WithUnknownId_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => _tool.ResolveGuid("does-not-exist"));
    }

    [Fact]
    public async Task GetMaterialDetailsAsync_WithoutBridge_ThrowsClientVisibleError()
    {
        var exception = await Assert.ThrowsAsync<McpException>(() =>
            _tool.GetMaterialDetailsAsync("material-id", TestContext.Current.CancellationToken));

        Assert.Equal(
            "No Flax Editor bridge session is available for the configured project.",
            exception.Message);
    }
}
