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

        Assert.Equal(new FlaxBridgeStatus(true, "1.2.3", 12000, startedUtc), status);
    }

    [Fact]
    public async Task GetStatusAsync_WithStaleHandshake_ReturnsDisconnected()
    {
        WriteHandshake("FlaxMcpTests-" + Guid.NewGuid().ToString("N"), DateTime.UtcNow);
        var client = new FlaxBridgeClient(_projectFolder, _tempDir);

        var status = await client.GetStatusAsync(TestContext.Current.CancellationToken);

        Assert.Equal(FlaxBridgeStatus.Disconnected, status);
    }

    private async Task ServePingAsync(string pipeName, CancellationToken cancellationToken)
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
        Assert.Contains("\"method\":\"ping\"", request);
        await writer.WriteLineAsync("{\"id\":1,\"result\":{\"pong\":true,\"utcNow\":\"2026-08-20T10:00:01Z\"}}");
    }

    private void WriteHandshake(string pipeName, DateTime startedUtc)
    {
        var handshakePath = Path.Combine(_tempDir, ProjectHash(_projectFolder) + ".json");
        File.WriteAllText(
            handshakePath,
            JsonSerializer.Serialize(
                new
                {
                    pipeName,
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
