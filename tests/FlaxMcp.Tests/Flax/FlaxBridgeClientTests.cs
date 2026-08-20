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

        Assert.Contains("server requires version 2", exception.Message);
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

    private void WriteHandshake(string pipeName, DateTime startedUtc, int protocolVersion = 2)
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
