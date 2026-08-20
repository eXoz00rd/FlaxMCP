using FlaxMcp.Flax;
using FlaxMcp.Tools;
using Xunit;

namespace FlaxMcp.Tests.Tools;

public sealed class EditorToolsTests
{
    [Fact]
    public async Task GetSceneGraphAsync_ReturnsBridgeResult()
    {
        var expected = new FlaxLiveSceneGraph(
            12,
            [new FlaxLiveSceneNode("scene-id", "FlaxEngine.Scene", "Main", [])],
            false
        );
        var tool = new EditorTools(new StubBridgeClient(expected));

        var result = await tool.GetSceneGraphAsync(TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
    }

    private sealed class StubBridgeClient : IFlaxBridgeClient
    {
        private readonly FlaxLiveSceneGraph _sceneGraph;

        public StubBridgeClient(FlaxLiveSceneGraph sceneGraph)
        {
            _sceneGraph = sceneGraph;
        }

        public Task<FlaxBridgeStatus> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<FlaxBridgePing> PingAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<FlaxLiveSceneGraph> GetSceneGraphAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_sceneGraph);
        }

        public Task<FlaxBridgeScreenshot> CaptureScreenshotAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
