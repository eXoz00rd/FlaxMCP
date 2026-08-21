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

    [Fact]
    public async Task GetActorDetailsAsync_ForwardsActorId()
    {
        var expected = CreateActorDetails();
        var bridge = new StubBridgeClient { ActorDetails = expected };
        var tool = new EditorTools(bridge);

        var result = await tool.GetActorDetailsAsync("actor-id", TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        Assert.Equal("actor-id", bridge.ActorId);
    }

    [Fact]
    public async Task ModifyActorAsync_ForwardsActorIdAndTransform()
    {
        var expected = CreateActorDetails();
        var bridge = new StubBridgeClient { ActorDetails = expected };
        var tool = new EditorTools(bridge);
        var translation = new FlaxVector3(10, 20, 30);
        var orientation = new FlaxQuaternion(0, 0, 0, 1);
        var scale = new FlaxVector3(1, 2, 3);

        var result = await tool.ModifyActorAsync(
            "actor-id",
            translation,
            orientation,
            scale,
            TestContext.Current.CancellationToken
        );

        Assert.Same(expected, result);
        Assert.Equal("actor-id", bridge.ActorId);
        Assert.Equal(new FlaxActorTransform(translation, orientation, scale), bridge.Transform);
    }

    [Fact]
    public async Task SaveAsync_ReturnsBridgeResult()
    {
        var expected = new FlaxEditorSaveResult(12, true);
        var bridge = new StubBridgeClient { SaveResult = expected };
        var tool = new EditorTools(bridge);

        var result = await tool.SaveAsync(TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task SetPlayModeAsync_ForwardsAction()
    {
        var expected = new FlaxEditorPlayModeResult(12, "pause", true, true);
        var bridge = new StubBridgeClient { PlayModeResult = expected };
        var tool = new EditorTools(bridge);

        var result = await tool.SetPlayModeAsync("pause", TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        Assert.Equal("pause", bridge.PlayModeAction);
    }

    [Fact]
    public async Task ExecuteCSharpAsync_ForwardsCode()
    {
        var expected = new FlaxCodeExecutionResult(12, "System.Int32", null);
        var bridge = new StubBridgeClient { CodeExecutionResult = expected };
        var tool = new EditorTools(bridge);

        var result = await tool.ExecuteCSharpAsync("return 42;", TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        Assert.Equal("return 42;", bridge.Code);
    }

    private static FlaxActorDetails CreateActorDetails()
    {
        var transform = new FlaxActorTransform(
            new FlaxVector3(1, 2, 3),
            new FlaxQuaternion(0, 0, 0, 1),
            new FlaxVector3(1, 1, 1)
        );
        return new FlaxActorDetails(
            12,
            "actor-id",
            "FlaxEngine.Actor",
            "Player",
            null,
            "scene-id",
            true,
            true,
            0,
            "Default",
            ["Player"],
            transform,
            transform,
            []
        );
    }

    private sealed class StubBridgeClient : IFlaxBridgeClient
    {
        private readonly FlaxLiveSceneGraph _sceneGraph;

        public FlaxEditorSelection Selection { get; set; } = new(0, []);

        public IReadOnlyList<string>? ActorIds { get; private set; }

        public FlaxActorDetails ActorDetails { get; set; } = CreateActorDetails();

        public string? ActorId { get; private set; }

        public FlaxActorTransform? Transform { get; private set; }

        public FlaxEditorSaveResult SaveResult { get; set; } = new(0, true);

        public FlaxEditorPlayModeResult PlayModeResult { get; set; } = new(0, "stop", false, false);

        public string? PlayModeAction { get; private set; }

        public FlaxCodeExecutionResult CodeExecutionResult { get; set; } = new(0, null, null);

        public string? Code { get; private set; }

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

        public Task<FlaxActorDetails> GetActorDetailsAsync(
            string actorId,
            CancellationToken cancellationToken = default)
        {
            ActorId = actorId;
            return Task.FromResult(ActorDetails);
        }

        public Task<FlaxActorDetails> ModifyActorAsync(
            string actorId,
            FlaxActorTransform transform,
            CancellationToken cancellationToken = default)
        {
            ActorId = actorId;
            Transform = transform;
            return Task.FromResult(ActorDetails);
        }

        public Task<FlaxEditorSaveResult> SaveAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(SaveResult);
        }

        public Task<FlaxEditorPlayModeResult> SetPlayModeAsync(
            string action,
            CancellationToken cancellationToken = default)
        {
            PlayModeAction = action;
            return Task.FromResult(PlayModeResult);
        }

        public Task<FlaxCodeExecutionResult> ExecuteCSharpAsync(
            string code,
            CancellationToken cancellationToken = default)
        {
            Code = code;
            return Task.FromResult(CodeExecutionResult);
        }
    }
}
