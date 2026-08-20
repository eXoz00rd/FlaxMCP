using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace FlaxMcp.Flax;

public interface IFlaxBridgeClient
{
    Task<FlaxBridgeStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<FlaxBridgePing> PingAsync(CancellationToken cancellationToken = default);

    Task<JsonElement> ListActorsAsync(CancellationToken cancellationToken = default);

    Task<FlaxBridgeScreenshot> CaptureScreenshotAsync(string path, CancellationToken cancellationToken = default);
}

public sealed class FlaxBridgeClient : IFlaxBridgeClient
{
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(1);
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly string _projectFolder;
    private readonly string _sessionsDirectory;

    public FlaxBridgeClient(IOptions<Configuration.FlaxMcpOptions> options)
        : this(
            Path.GetDirectoryName(options.Value.ResolveProjectFile())!,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FlaxMcp", "sessions")
        )
    {
    }

    internal FlaxBridgeClient(string projectFolder, string sessionsDirectory)
    {
        _projectFolder = Path.GetFullPath(projectFolder);
        _sessionsDirectory = sessionsDirectory;
    }

    public async Task<FlaxBridgeStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        FlaxBridgeHandshake? handshake;
        try
        {
            handshake = ReadHandshake();
            if (handshake is null)
            {
                return FlaxBridgeStatus.Disconnected;
            }

            await PingAsync(handshake, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return FlaxBridgeStatus.Disconnected;
        }
        catch (Exception ex) when (ex is IOException or JsonException or TimeoutException)
        {
            return FlaxBridgeStatus.Disconnected;
        }

        return new FlaxBridgeStatus(true, handshake.PluginVersion, handshake.EngineBuild, handshake.StartedUtc);
    }

    public Task<FlaxBridgePing> PingAsync(CancellationToken cancellationToken = default)
    {
        return PingAsync(RequireHandshake(), cancellationToken);
    }

    public Task<JsonElement> ListActorsAsync(CancellationToken cancellationToken = default)
    {
        return CallAsync<JsonElement>(RequireHandshake(), "list_actors", null, cancellationToken);
    }

    public Task<FlaxBridgeScreenshot> CaptureScreenshotAsync(string path, CancellationToken cancellationToken = default)
    {
        return CallAsync<FlaxBridgeScreenshot>(RequireHandshake(), "screenshot", new { path }, cancellationToken);
    }

    private Task<FlaxBridgePing> PingAsync(FlaxBridgeHandshake handshake, CancellationToken cancellationToken)
    {
        return CallAsync<FlaxBridgePing>(handshake, "ping", null, cancellationToken);
    }

    private async Task<T> CallAsync<T>(
        FlaxBridgeHandshake handshake,
        string method,
        object? parameters,
        CancellationToken cancellationToken)
    {
        using var pipe = new NamedPipeClientStream(
            ".",
            handshake.PipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous
        );
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ConnectionTimeout);

        try
        {
            await pipe.ConnectAsync(timeout.Token);
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

            await writer.WriteLineAsync(JsonSerializer.Serialize(new { id = 1, method, @params = parameters }));
            var responseLine = await reader.ReadLineAsync(timeout.Token) ??
                throw new IOException("The Flax Editor bridge disconnected before returning a response.");
            using var response = JsonDocument.Parse(responseLine);
            var root = response.RootElement;
            if (root.TryGetProperty("error", out var error))
            {
                throw new InvalidOperationException($"Flax Editor bridge error: {error.GetString()}");
            }

            return root.GetProperty("result").Deserialize<T>(SerializerOptions) ??
                throw new JsonException("The Flax Editor bridge returned an empty result.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"The Flax Editor bridge did not respond within {ConnectionTimeout.TotalSeconds:0} second."
            );
        }
    }

    private FlaxBridgeHandshake RequireHandshake()
    {
        return ReadHandshake() ??
            throw new InvalidOperationException(
                $"No Flax Editor bridge session is available for '{_projectFolder}'."
            );
    }

    private FlaxBridgeHandshake? ReadHandshake()
    {
        var path = Path.Combine(_sessionsDirectory, Hash(_projectFolder) + ".json");
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<FlaxBridgeHandshake>(
            File.ReadAllText(path),
            SerializerOptions
        );
    }

    private static string Hash(string value)
    {
        var normalized = value.Replace('\\', '/').TrimEnd('/').ToLowerInvariant();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes)[..16];
    }

    private sealed record FlaxBridgeHandshake(
        string PipeName,
        string PluginVersion,
        int EngineBuild,
        DateTime StartedUtc);
}

public sealed record FlaxBridgeStatus(bool Connected, string? PluginVersion, int? EngineBuild, DateTime? StartedUtc)
{
    public static FlaxBridgeStatus Disconnected { get; } = new(false, null, null, null);
}

public sealed record FlaxBridgePing(bool Pong, DateTime UtcNow);

public sealed record FlaxBridgeScreenshot(string Path, long Bytes);
