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

    [Fact]
    public async Task GetSelectionAsync_ReturnsBridgeResult()
    {
        var expected = new FlaxEditorSelection(
            12,
            [new FlaxEditorSelectionItem("actor-id", "FlaxEngine.Actor", "Player")]
        );
        var bridge = new StubBridgeClient();
        bridge.Selection = expected;
        var tool = new EditorTools(bridge);

        var result = await tool.GetSelectionAsync(TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task SetSelectionAsync_ForwardsActorIds()
    {
        var expected = new FlaxEditorSelection(12, []);
        var bridge = new StubBridgeClient { Selection = expected };
        var tool = new EditorTools(bridge);

        var result = await tool.SetSelectionAsync(["actor-id"], TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        Assert.Equal(["actor-id"], bridge.ActorIds);
    }

    private sealed class StubBridgeClient : IFlaxBridgeClient
    {
        private readonly FlaxLiveSceneGraph _sceneGraph;

        public FlaxEditorSelection Selection { get; set; } = new(0, []);

        public IReadOnlyList<string>? ActorIds { get; private set; }

        public StubBridgeClient()
            : this(new FlaxLiveSceneGraph(0, [], false))
        {
        }

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

        public Task<FlaxEditorSelection> GetSelectionAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Selection);
        }

        public Task<FlaxEditorSelection> SetSelectionAsync(
            IReadOnlyList<string> actorIds,
            CancellationToken cancellationToken = default)
        {
            ActorIds = actorIds;
            return Task.FromResult(Selection);
        }

        public Task<FlaxBridgeScreenshot> CaptureScreenshotAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
