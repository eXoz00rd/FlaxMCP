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

    Task<FlaxLiveSceneGraph> GetSceneGraphAsync(CancellationToken cancellationToken = default);

    Task<FlaxEditorSelection> GetSelectionAsync(CancellationToken cancellationToken = default);

    Task<FlaxEditorSelection> SetSelectionAsync(
        IReadOnlyList<string> actorIds,
        CancellationToken cancellationToken = default);

    Task<FlaxActorDetails> GetActorDetailsAsync(string actorId, CancellationToken cancellationToken = default);

    Task<FlaxActorDetails> ModifyActorAsync(
        string actorId,
        FlaxActorTransform transform,
        CancellationToken cancellationToken = default);

    Task<FlaxEditorSaveResult> SaveAsync(CancellationToken cancellationToken = default);

    Task<FlaxBridgeScreenshot> CaptureScreenshotAsync(string path, CancellationToken cancellationToken = default);
}

public sealed class FlaxBridgeClient : IFlaxBridgeClient
{
    internal const int CurrentProtocolVersion = 4;
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

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
        FlaxBridgeHandshake? handshake = null;
        try
        {
            handshake = ReadHandshake();
            if (handshake is null)
            {
                return FlaxBridgeStatus.Disconnected;
            }

            ValidateProtocol(handshake);
            await PingAsync(handshake, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return FlaxBridgeStatus.Disconnected;
        }
        catch (FlaxBridgeProtocolException ex)
        {
            return new FlaxBridgeStatus(
                false,
                handshake?.PluginVersion,
                handshake?.EngineBuild,
                handshake?.StartedUtc,
                ex.Message
            );
        }
        catch (Exception ex) when (ex is IOException
                                       or JsonException
                                       or TimeoutException
                                       or InvalidOperationException
                                       or KeyNotFoundException)
        {
            return FlaxBridgeStatus.Disconnected;
        }

        return new FlaxBridgeStatus(
            true,
            handshake.PluginVersion,
            handshake.EngineBuild,
            handshake.StartedUtc,
            null
        );
    }

    public Task<FlaxBridgePing> PingAsync(CancellationToken cancellationToken = default)
    {
        return PingAsync(RequireHandshake(), cancellationToken);
    }

    public Task<FlaxLiveSceneGraph> GetSceneGraphAsync(CancellationToken cancellationToken = default)
    {
        return CallAsync<FlaxLiveSceneGraph>(RequireHandshake(), "scene_graph", null, cancellationToken);
    }

    public Task<FlaxEditorSelection> GetSelectionAsync(CancellationToken cancellationToken = default)
    {
        return CallAsync<FlaxEditorSelection>(RequireHandshake(), "get_selection", null, cancellationToken);
    }

    public Task<FlaxEditorSelection> SetSelectionAsync(
        IReadOnlyList<string> actorIds,
        CancellationToken cancellationToken = default)
    {
        return CallAsync<FlaxEditorSelection>(
            RequireHandshake(),
            "set_selection",
            new { actorIds },
            cancellationToken
        );
    }

    public Task<FlaxActorDetails> GetActorDetailsAsync(
        string actorId,
        CancellationToken cancellationToken = default)
    {
        return CallAsync<FlaxActorDetails>(
            RequireHandshake(),
            "actor_details",
            new { actorId },
            cancellationToken
        );
    }

    public Task<FlaxActorDetails> ModifyActorAsync(
        string actorId,
        FlaxActorTransform transform,
        CancellationToken cancellationToken = default)
    {
        return CallAsync<FlaxActorDetails>(
            RequireHandshake(),
            "modify_actor",
            new { actorId, transform },
            cancellationToken
        );
    }

    public Task<FlaxEditorSaveResult> SaveAsync(CancellationToken cancellationToken = default)
    {
        return CallAsync<FlaxEditorSaveResult>(RequireHandshake(), "save", null, cancellationToken);
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
        ValidateProtocol(handshake);
        using var connectionTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        connectionTimeout.CancelAfter(ConnectionTimeout);
        var connected = false;

        try
        {
            await pipe.ConnectAsync(connectionTimeout.Token);
            connected = true;
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

            using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            requestTimeout.CancelAfter(RequestTimeout);
            await writer.WriteLineAsync(
                JsonSerializer.Serialize(
                    new { protocolVersion = CurrentProtocolVersion, id = 1, method, @params = parameters },
                    SerializerOptions
                )
            );
            var responseLine = await reader.ReadLineAsync(requestTimeout.Token) ??
                throw new IOException("The Flax Editor bridge disconnected before returning a response.");
            using var response = JsonDocument.Parse(responseLine);
            var root = response.RootElement;
            if (root.TryGetProperty("error", out var error))
            {
                var bridgeError = error.Deserialize<FlaxBridgeError>(SerializerOptions) ??
                    throw new JsonException("The Flax Editor bridge returned an invalid error response.");
                throw new InvalidOperationException(
                    $"Flax Editor bridge error [{bridgeError.Code}]: {bridgeError.Message}"
                );
            }

            return root.GetProperty("result").Deserialize<T>(SerializerOptions) ??
                throw new JsonException("The Flax Editor bridge returned an empty result.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var timeout = connected ?
                RequestTimeout :
                ConnectionTimeout;
            throw new TimeoutException(
                $"The Flax Editor bridge did not respond within {timeout.TotalSeconds:0} " +
                $"{(timeout == ConnectionTimeout ? "second" : "seconds")}."
            );
        }
        catch (IOException ex) when (connected)
        {
            throw new IOException("The Flax Editor bridge disconnected before returning a response.", ex);
        }
    }

    private FlaxBridgeHandshake RequireHandshake()
    {
        return ReadHandshake() ??
            throw new InvalidOperationException(
                $"No Flax Editor bridge session is available for '{_projectFolder}'."
            );
    }

    private static void ValidateProtocol(FlaxBridgeHandshake handshake)
    {
        if (handshake.ProtocolVersion != CurrentProtocolVersion)
        {
            throw new FlaxBridgeProtocolException(
                $"Flax Editor bridge protocol mismatch: server requires version {CurrentProtocolVersion}, " +
                $"but the editor plugin reports version {handshake.ProtocolVersion}."
            );
        }
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
        int ProtocolVersion,
        string PluginVersion,
        int EngineBuild,
        DateTime StartedUtc);
}

public sealed record FlaxBridgeStatus(
    bool Connected,
    string? PluginVersion,
    int? EngineBuild,
    DateTime? StartedUtc,
    string? Error = null)
{
    public static FlaxBridgeStatus Disconnected { get; } = new(
        false,
        null,
        null,
        null,
        null
    );
}

public sealed record FlaxBridgePing(bool Pong, DateTime UtcNow);

public sealed record FlaxBridgeScreenshot(string Path, long Bytes);

public sealed record FlaxEditorSaveResult(int MainThreadId, bool Saved);

public sealed record FlaxLiveSceneGraph(
    int MainThreadId,
    IReadOnlyList<FlaxLiveSceneNode> Scenes,
    bool Truncated);

public sealed record FlaxLiveSceneNode(
    string Id,
    string TypeName,
    string? Name,
    IReadOnlyList<FlaxLiveSceneNode> Children);

public sealed record FlaxEditorSelection(
    int MainThreadId,
    IReadOnlyList<FlaxEditorSelectionItem> Selected);

public sealed record FlaxEditorSelectionItem(string Id, string TypeName, string Name);

public sealed record FlaxActorDetails(
    int MainThreadId,
    string Id,
    string TypeName,
    string Name,
    string? ParentId,
    string? SceneId,
    bool IsActive,
    bool IsActiveInHierarchy,
    int Layer,
    string LayerName,
    IReadOnlyList<string> Tags,
    FlaxActorTransform Transform,
    FlaxActorTransform LocalTransform,
    IReadOnlyList<FlaxActorScript> Scripts);

public sealed record FlaxActorTransform(
    FlaxVector3 Translation,
    FlaxQuaternion Orientation,
    FlaxVector3 Scale);

public sealed record FlaxVector3(double X, double Y, double Z);

public sealed record FlaxQuaternion(double X, double Y, double Z, double W);

public sealed record FlaxActorScript(string Id, string TypeName, bool Enabled, bool IsEnabledInHierarchy);

internal sealed record FlaxBridgeError(string Code, string Message);

internal sealed class FlaxBridgeProtocolException : InvalidOperationException
{
    public FlaxBridgeProtocolException(string message)
        : base(message)
    {
    }
}
