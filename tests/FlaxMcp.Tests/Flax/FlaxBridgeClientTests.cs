using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FlaxMcp.Flax;
using Xunit;

namespace FlaxMcp.Tests.Flax;

public sealed class FlaxBridgeClientTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "FlaxMcpTests_" + Guid.NewGuid().ToString("N"));

    private readonly string _projectFolder = Path.Combine(
        Path.GetTempPath(),
        "FlaxProject_" + Guid.NewGuid().ToString("N")
    );

    public FlaxBridgeClientTests()
    {
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(_projectFolder);
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
        Directory.Delete(_projectFolder, recursive: true);
    }

    [Fact]
    public async Task GetStatusAsync_WithoutHandshake_ReturnsDisconnected()
    {
        var client = new FlaxBridgeClient(_projectFolder, _tempDir);

        var status = await client.GetStatusAsync(TestContext.Current.CancellationToken);

        Assert.Equal(FlaxBridgeStatus.Disconnected, status);
    }

    [Fact]
    public async Task GetStatusAsync_WithReachableBridge_ReturnsHandshakeMetadata()
    {
        var pipeName = "FlaxMcpTests-" + Guid.NewGuid().ToString("N");
        var startedUtc = new DateTime(
            2026,
            8,
            20,
            10,
            0,
            0,
            DateTimeKind.Utc
        );
        WriteHandshake(pipeName, startedUtc);
        var serverTask = ServePingAsync(pipeName, TestContext.Current.CancellationToken);
        var client = new FlaxBridgeClient(_projectFolder, _tempDir);

        var status = await client.GetStatusAsync(TestContext.Current.CancellationToken);
        await serverTask;

        Assert.Equal(
            new FlaxBridgeStatus(
                true,
                "1.2.3",
                12000,
                startedUtc,
                null
            ),
            status
        );
    }

    [Fact]
    public async Task GetStatusAsync_WithStaleHandshake_ReturnsDisconnected()
    {
        WriteHandshake("FlaxMcpTests-" + Guid.NewGuid().ToString("N"), DateTime.UtcNow);
        var client = new FlaxBridgeClient(_projectFolder, _tempDir);

        var status = await client.GetStatusAsync(TestContext.Current.CancellationToken);

        Assert.Equal(FlaxBridgeStatus.Disconnected, status);
    }

    [Theory]
    [InlineData("{\"id\":1,\"error\":{\"code\":\"action_failed\",\"message\":\"Editor action failed\"}}")]
    [InlineData("{\"id\":1}")]
    public async Task GetStatusAsync_WithInvalidBridgeResponse_ReturnsDisconnected(string response)
    {
        var pipeName = "FlaxMcpTests-" + Guid.NewGuid().ToString("N");
        WriteHandshake(pipeName, DateTime.UtcNow);
        var serverTask = ServeResponseAsync(pipeName, response, TestContext.Current.CancellationToken);
        var client = new FlaxBridgeClient(_projectFolder, _tempDir);

        var status = await client.GetStatusAsync(TestContext.Current.CancellationToken);
        await serverTask;

        Assert.Equal(FlaxBridgeStatus.Disconnected, status);
    }

    [Fact]
    public async Task PingAsync_WithMismatchedProtocol_ReportsVersions()
    {
        WriteHandshake("unused", DateTime.UtcNow, protocolVersion: 3);
        var client = new FlaxBridgeClient(_projectFolder, _tempDir);

        var exception =
            await Assert.ThrowsAnyAsync<InvalidOperationException>(()
                => client.PingAsync(TestContext.Current.CancellationToken)
            );

        Assert.Contains(
            $"server requires version {FlaxBridgeClient.CurrentProtocolVersion}",
            exception.Message);
        Assert.Contains("plugin reports version 3", exception.Message);
    }

    [Fact]
    public async Task PingAsync_WithStructuredError_ReportsCodeAndMessage()
    {
        var pipeName = "FlaxMcpTests-" + Guid.NewGuid().ToString("N");
        WriteHandshake(pipeName, DateTime.UtcNow);
        var serverTask = ServeResponseAsync(
            pipeName,
            "{\"id\":1,\"error\":{\"code\":\"action_failed\",\"message\":\"Editor action failed\"}}",
            TestContext.Current.CancellationToken
        );
        var client = new FlaxBridgeClient(_projectFolder, _tempDir);

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(()
                => client.PingAsync(TestContext.Current.CancellationToken)
            );
        await serverTask;

        Assert.Contains("[action_failed]: Editor action failed", exception.Message);
    }

    [Fact]
    public async Task PingAsync_AfterHandshakeChanges_ConnectsToNewSession()
    {
        WriteHandshake("stale-" + Guid.NewGuid().ToString("N"), DateTime.UtcNow);
        var client = new FlaxBridgeClient(_projectFolder, _tempDir);
        await Assert.ThrowsAnyAsync<Exception>(() => client.PingAsync(TestContext.Current.CancellationToken));

        var pipeName = "FlaxMcpTests-" + Guid.NewGuid().ToString("N");
        WriteHandshake(pipeName, DateTime.UtcNow);
        var serverTask = ServePingAsync(pipeName, TestContext.Current.CancellationToken);

        var ping = await client.PingAsync(TestContext.Current.CancellationToken);
        await serverTask;

        Assert.True(ping.Pong);
    }

    [Fact]
    public async Task PingAsync_WhenBridgeDisconnects_ReportsClearError()
    {
        var pipeName = "FlaxMcpTests-" + Guid.NewGuid().ToString("N");
        WriteHandshake(pipeName, DateTime.UtcNow);
        var serverTask = ServeDisconnectAsync(pipeName, TestContext.Current.CancellationToken);
        var client = new FlaxBridgeClient(_projectFolder, _tempDir);

        var exception =
            await Assert.ThrowsAsync<IOException>(() => client.PingAsync(TestContext.Current.CancellationToken)
            );
        await serverTask;

        Assert.Contains("disconnected before returning a response", exception.Message);
    }

    [Fact]
    public async Task GetSceneGraphAsync_ReturnsTypedLiveTree()
    {
        var pipeName = "FlaxMcpTests-" + Guid.NewGuid().ToString("N");
        WriteHandshake(pipeName, DateTime.UtcNow);
        var serverTask = ServeResponseAsync(
            pipeName,
            "{\"id\":1,\"result\":{\"mainThreadId\":7,\"scenes\":[{\"id\":\"scene-id\",\"typeName\":\"FlaxEngine.Scene\",\"name\":\"Main\",\"children\":[{\"id\":\"actor-id\",\"typeName\":\"FlaxEngine.Actor\",\"name\":\"Player\",\"children\":[]}]}],\"truncated\":false}}",
            TestContext.Current.CancellationToken,
            "scene_graph"
        );
        var client = new FlaxBridgeClient(_projectFolder, _tempDir);

        var graph = await client.GetSceneGraphAsync(TestContext.Current.CancellationToken);
        await serverTask;

        Assert.Equal(7, graph.MainThreadId);
        var scene = Assert.Single(graph.Scenes);
        Assert.Equal("Main", scene.Name);
        Assert.Equal("Player", Assert.Single(scene.Children).Name);
        Assert.False(graph.Truncated);
    }

    [Fact]
    public async Task GetSelectionAsync_ReturnsTypedSelection()
    {
        var pipeName = "FlaxMcpTests-" + Guid.NewGuid().ToString("N");
        WriteHandshake(pipeName, DateTime.UtcNow);
        var serverTask = ServeResponseAsync(
            pipeName,
            "{\"id\":1,\"result\":{\"mainThreadId\":7,\"selected\":[{\"id\":\"actor-id\",\"typeName\":\"FlaxEngine.Actor\",\"name\":\"Player\"}]}}",
            TestContext.Current.CancellationToken,
            "get_selection"
        );
        var client = new FlaxBridgeClient(_projectFolder, _tempDir);

        var selection = await client.GetSelectionAsync(TestContext.Current.CancellationToken);
        await serverTask;

        Assert.Equal(7, selection.MainThreadId);
        Assert.Equal("Player", Assert.Single(selection.Selected).Name);
    }

    [Fact]
    public async Task SetSelectionAsync_SendsActorIdsAndReturnsTypedSelection()
    {
        var pipeName = "FlaxMcpTests-" + Guid.NewGuid().ToString("N");
        WriteHandshake(pipeName, DateTime.UtcNow);
        var serverTask = ServeResponseAsync(
            pipeName,
            "{\"id\":1,\"result\":{\"mainThreadId\":7,\"selected\":[]}}",
            TestContext.Current.CancellationToken,
            "set_selection",
            "\"actorIds\":[\"actor-id\"]"
        );
        var client = new FlaxBridgeClient(_projectFolder, _tempDir);

        var selection = await client.SetSelectionAsync(["actor-id"], TestContext.Current.CancellationToken);
        await serverTask;

        Assert.Empty(selection.Selected);
    }

    [Fact]
    public async Task GetActorDetailsAsync_ReturnsTypedActorDetails()
    {
        var pipeName = "FlaxMcpTests-" + Guid.NewGuid().ToString("N");
        WriteHandshake(pipeName, DateTime.UtcNow);
        var serverTask = ServeResponseAsync(
            pipeName,
            "{\"id\":1,\"result\":{\"mainThreadId\":7,\"id\":\"actor-id\",\"typeName\":\"FlaxEngine.Actor\",\"name\":\"Player\",\"parentId\":null,\"sceneId\":\"scene-id\",\"isActive\":true,\"isActiveInHierarchy\":true,\"layer\":0,\"layerName\":\"Default\",\"tags\":[\"Player\"],\"transform\":{\"translation\":{\"x\":1,\"y\":2,\"z\":3},\"orientation\":{\"x\":0,\"y\":0,\"z\":0,\"w\":1},\"scale\":{\"x\":1,\"y\":1,\"z\":1}},\"localTransform\":{\"translation\":{\"x\":1,\"y\":2,\"z\":3},\"orientation\":{\"x\":0,\"y\":0,\"z\":0,\"w\":1},\"scale\":{\"x\":1,\"y\":1,\"z\":1}},\"scripts\":[{\"id\":\"script-id\",\"typeName\":\"Game.Player\",\"enabled\":true,\"isEnabledInHierarchy\":true}]}}",
            TestContext.Current.CancellationToken,
            "actor_details",
            "\"actorId\":\"actor-id\""
        );
        var client = new FlaxBridgeClient(_projectFolder, _tempDir);

        var details = await client.GetActorDetailsAsync("actor-id", TestContext.Current.CancellationToken);
        await serverTask;

        Assert.Equal("Player", details.Name);
        Assert.Equal(3, details.Transform.Translation.Z);
        Assert.Equal("Game.Player", Assert.Single(details.Scripts).TypeName);
    }

    [Fact]
    public async Task ModifyActorAsync_SendsTransformAndReturnsTypedActorDetails()
    {
        var pipeName = "FlaxMcpTests-" + Guid.NewGuid().ToString("N");
        WriteHandshake(pipeName, DateTime.UtcNow);
        var response =
            "{\"id\":1,\"result\":{\"mainThreadId\":7,\"id\":\"actor-id\",\"typeName\":\"FlaxEngine.Actor\",\"name\":\"Player\",\"parentId\":null,\"sceneId\":\"scene-id\",\"isActive\":true,\"isActiveInHierarchy\":true,\"layer\":0,\"layerName\":\"Default\",\"tags\":[],\"transform\":{\"translation\":{\"x\":10,\"y\":20,\"z\":30},\"orientation\":{\"x\":0,\"y\":0,\"z\":0,\"w\":1},\"scale\":{\"x\":1,\"y\":1,\"z\":1}},\"localTransform\":{\"translation\":{\"x\":10,\"y\":20,\"z\":30},\"orientation\":{\"x\":0,\"y\":0,\"z\":0,\"w\":1},\"scale\":{\"x\":1,\"y\":1,\"z\":1}},\"scripts\":[]}}";
        var serverTask = ServeResponseAsync(
            pipeName,
            response,
            TestContext.Current.CancellationToken,
            "modify_actor",
            "\"translation\":{\"x\":10,\"y\":20,\"z\":30}"
        );
        var client = new FlaxBridgeClient(_projectFolder, _tempDir);
        var transform = new FlaxActorTransform(
            new FlaxVector3(10, 20, 30),
            new FlaxQuaternion(0, 0, 0, 1),
            new FlaxVector3(1, 1, 1)
        );

        var details = await client.ModifyActorAsync(
            "actor-id",
            transform,
            TestContext.Current.CancellationToken
        );
        await serverTask;

        Assert.Equal(30, details.Transform.Translation.Z);
    }

    [Theory]
    [InlineData(true, "create_actor", "\"actorType\":\"EmptyActor\"")]
    [InlineData(false, "duplicate_actor", "\"actorId\":\"source-id\"")]
    public async Task ActorCreationMethods_SendTypedRequests(
        bool create,
        string method,
        string expectedRequestText)
    {
        var pipeName = "FlaxMcpTests-" + Guid.NewGuid().ToString("N");
        WriteHandshake(pipeName, DateTime.UtcNow);
        var response =
            "{\"id\":1,\"result\":{\"mainThreadId\":7,\"id\":\"new-id\",\"typeName\":\"FlaxEngine.EmptyActor\",\"name\":\"Room\",\"parentId\":null,\"sceneId\":\"scene-id\",\"isActive\":true,\"isActiveInHierarchy\":true,\"layer\":0,\"layerName\":\"Default\",\"tags\":[],\"transform\":{\"translation\":{\"x\":1,\"y\":2,\"z\":3},\"orientation\":{\"x\":0,\"y\":0,\"z\":0,\"w\":1},\"scale\":{\"x\":1,\"y\":1,\"z\":1}},\"localTransform\":{\"translation\":{\"x\":1,\"y\":2,\"z\":3},\"orientation\":{\"x\":0,\"y\":0,\"z\":0,\"w\":1},\"scale\":{\"x\":1,\"y\":1,\"z\":1}},\"scripts\":[]}}";
        var serverTask = ServeResponseAsync(pipeName, response, TestContext.Current.CancellationToken,
            method, expectedRequestText);
        var client = new FlaxBridgeClient(_projectFolder, _tempDir);
        var transform = new FlaxActorTransform(new FlaxVector3(1, 2, 3),
            new FlaxQuaternion(0, 0, 0, 1), new FlaxVector3(1, 1, 1));

        var details = create
            ? await client.CreateActorAsync("EmptyActor", "Room", "scene-id", null, transform,
                TestContext.Current.CancellationToken)
            : await client.DuplicateActorAsync("source-id", "Room", "scene-id", null, transform,
                TestContext.Current.CancellationToken);
        await serverTask;

        Assert.Equal("new-id", details.Id);
    }

    [Theory]
    [InlineData(true, "rename_actor", "\"name\":\"Wall\"")]
    [InlineData(false, "reparent_actor", "\"preserveWorldTransform\":true")]
    public async Task ActorHierarchyMethods_SendTypedRequests(
        bool rename,
        string method,
        string expectedRequestText)
    {
        var pipeName = "FlaxMcpTests-" + Guid.NewGuid().ToString("N");
        WriteHandshake(pipeName, DateTime.UtcNow);
        var response =
            "{\"id\":1,\"result\":{\"mainThreadId\":7,\"id\":\"actor-id\",\"typeName\":\"FlaxEngine.EmptyActor\",\"name\":\"Wall\",\"parentId\":\"parent-id\",\"sceneId\":\"scene-id\",\"isActive\":true,\"isActiveInHierarchy\":true,\"layer\":0,\"layerName\":\"Default\",\"tags\":[],\"transform\":{\"translation\":{\"x\":1,\"y\":2,\"z\":3},\"orientation\":{\"x\":0,\"y\":0,\"z\":0,\"w\":1},\"scale\":{\"x\":1,\"y\":1,\"z\":1}},\"localTransform\":{\"translation\":{\"x\":1,\"y\":2,\"z\":3},\"orientation\":{\"x\":0,\"y\":0,\"z\":0,\"w\":1},\"scale\":{\"x\":1,\"y\":1,\"z\":1}},\"scripts\":[]}}";
        var serverTask = ServeResponseAsync(pipeName, response, TestContext.Current.CancellationToken,
            method, expectedRequestText);
        var client = new FlaxBridgeClient(_projectFolder, _tempDir);

        var details = rename
            ? await client.RenameActorAsync("actor-id", "Wall", TestContext.Current.CancellationToken)
            : await client.ReparentActorAsync("actor-id", null, "parent-id", true,
                TestContext.Current.CancellationToken);
        await serverTask;

        Assert.Equal("actor-id", details.Id);
    }

    [Fact]
    public async Task DeleteActorAsync_SendsExplicitScopeAndReturnsDeletedIds()
    {
        var pipeName = "FlaxMcpTests-" + Guid.NewGuid().ToString("N");
        WriteHandshake(pipeName, DateTime.UtcNow);
        var serverTask = ServeResponseAsync(
            pipeName,
            "{\"id\":1,\"result\":{\"mainThreadId\":7,\"actorId\":\"actor-id\",\"deletedDescendants\":true,\"deletedActorIds\":[\"actor-id\",\"child-id\"]}}",
            TestContext.Current.CancellationToken,
            "delete_actor",
            "\"deleteDescendants\":true"
        );
        var client = new FlaxBridgeClient(_projectFolder, _tempDir);

        var result = await client.DeleteActorAsync(
            "actor-id", true, TestContext.Current.CancellationToken);
        await serverTask;

        Assert.True(result.DeletedDescendants);
        Assert.Equal(["actor-id", "child-id"], result.DeletedActorIds);
    }

    [Fact]
    public async Task SaveAsync_ReturnsTypedSaveResult()
    {
        var pipeName = "FlaxMcpTests-" + Guid.NewGuid().ToString("N");
        WriteHandshake(pipeName, DateTime.UtcNow);
        var serverTask = ServeResponseAsync(
            pipeName,
            "{\"id\":1,\"result\":{\"mainThreadId\":7,\"saved\":true}}",
            TestContext.Current.CancellationToken,
            "save"
        );
        var client = new FlaxBridgeClient(_projectFolder, _tempDir);

        var result = await client.SaveAsync(TestContext.Current.CancellationToken);
        await serverTask;

        Assert.Equal(new FlaxEditorSaveResult(7, true), result);
    }

    [Fact]
    public async Task SetPlayModeAsync_SendsActionAndReturnsTypedResult()
    {
        var pipeName = "FlaxMcpTests-" + Guid.NewGuid().ToString("N");
        WriteHandshake(pipeName, DateTime.UtcNow);
        var serverTask = ServeResponseAsync(
            pipeName,
            "{\"id\":1,\"result\":{\"mainThreadId\":7,\"requestedAction\":\"pause\",\"isPlayMode\":true,\"isPaused\":true}}",
            TestContext.Current.CancellationToken,
            "play_mode",
            "\"action\":\"pause\""
        );
        var client = new FlaxBridgeClient(_projectFolder, _tempDir);

        var result = await client.SetPlayModeAsync("pause", TestContext.Current.CancellationToken);
        await serverTask;

        Assert.Equal(new FlaxEditorPlayModeResult(7, "pause", true, true), result);
    }

    [Fact]
    public async Task ExecuteCSharpAsync_SendsCodeAndReturnsTypedResult()
    {
        var pipeName = "FlaxMcpTests-" + Guid.NewGuid().ToString("N");
        WriteHandshake(pipeName, DateTime.UtcNow);
        var serverTask = ServeResponseAsync(
            pipeName,
            "{\"id\":1,\"result\":{\"mainThreadId\":7,\"typeName\":\"System.Int32\",\"result\":42}}",
            TestContext.Current.CancellationToken,
            "execute_csharp",
            "\"code\":\"return 42;\""
        );
        var client = new FlaxBridgeClient(_projectFolder, _tempDir);

        var result = await client.ExecuteCSharpAsync("return 42;", TestContext.Current.CancellationToken);
        await serverTask;

        Assert.Equal(7, result.MainThreadId);
        Assert.Equal("System.Int32", result.TypeName);
        Assert.Equal(42, result.Result?.GetInt32());
    }

    private async Task ServePingAsync(string pipeName, CancellationToken cancellationToken)
    {
        await ServeResponseAsync(
            pipeName,
            "{\"id\":1,\"result\":{\"pong\":true,\"utcNow\":\"2026-08-20T10:00:01Z\"}}",
            cancellationToken
        );
    }

    private static async Task ServeResponseAsync(
        string pipeName,
        string response,
        CancellationToken cancellationToken,
        string method = "ping",
        string? expectedRequestText = null)
    {
        await using var pipe = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous
        );
        await pipe.WaitForConnectionAsync(cancellationToken);
        using var reader = new StreamReader(
            pipe,
            Encoding.UTF8,
            false,
            4096,
            leaveOpen: true
        );
        await using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, leaveOpen: true)
        {
            AutoFlush = true,
            NewLine = "\n",
        };
        var request = await reader.ReadLineAsync(cancellationToken);
        Assert.Contains($"\"method\":\"{method}\"", request);
        if (expectedRequestText is not null)
        {
            Assert.Contains(expectedRequestText, request);
        }

        await writer.WriteLineAsync(response);
    }

    private static async Task ServeDisconnectAsync(string pipeName, CancellationToken cancellationToken)
    {
        await using var pipe = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous
        );
        await pipe.WaitForConnectionAsync(cancellationToken);
        using var reader = new StreamReader(
            pipe,
            Encoding.UTF8,
            false,
            4096,
            leaveOpen: true
        );
        await reader.ReadLineAsync(cancellationToken);
    }

    private void WriteHandshake(string pipeName, DateTime startedUtc,
        int protocolVersion = FlaxBridgeClient.CurrentProtocolVersion)
    {
        var handshakePath = Path.Combine(_tempDir, ProjectHash(_projectFolder) + ".json");
        File.WriteAllText(
            handshakePath,
            JsonSerializer.Serialize(
                new
                {
                    pipeName,
                    protocolVersion,
                    pluginVersion = "1.2.3",
                    engineBuild = 12000,
                    startedUtc,
                }
            )
        );
    }

    private static string ProjectHash(string projectFolder)
    {
        var normalized = Path.GetFullPath(projectFolder).Replace('\\', '/').TrimEnd('/').ToLowerInvariant();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes)[..16];
    }
}
