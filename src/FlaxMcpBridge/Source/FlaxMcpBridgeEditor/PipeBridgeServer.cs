using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FlaxEngine;
using FlaxEditor.Windows;

namespace FlaxMcpBridge.Editor;

/// <summary>
/// Named-pipe bridge transport accepting one client connection at a time and exchanging
/// newline-delimited JSON messages.
/// </summary>
internal sealed class PipeBridgeServer : IDisposable
{
    private const int ProtocolVersion = 1;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);
    private readonly string _projectFolder;
    private readonly CancellationTokenSource _cts = new();
    private Task? _acceptLoop;
    private NamedPipeServerStream? _currentPipe;
    private string? _handshakePath;

    public string PipeName { get; }

    public PipeBridgeServer(string projectFolder)
    {
        _projectFolder = projectFolder;
        PipeName = "FlaxMcp-" + Hash(projectFolder);
    }

    public void Start()
    {
        WriteHandshakeFile();
        _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            NamedPipeServerStream? pipe = null;
            try
            {
                pipe = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                _currentPipe = pipe;
                await pipe.WaitForConnectionAsync(token);
                await HandleClientAsync(pipe, token);
            }
            catch (OperationCanceledException)
            {
                // Shutting down.
            }
            catch (IOException)
            {
                // Client disconnected mid-message; loop and accept the next connection.
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FlaxMcpBridge] Pipe connection error: {ex}");
            }
            finally
            {
                pipe?.Dispose();
                _currentPipe = null;
            }
        }
    }

    private static async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken token)
    {
        using var reader = new StreamReader(pipe, Encoding.UTF8, false, 4096, leaveOpen: true);
        await using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, leaveOpen: true) { AutoFlush = false, NewLine = "\n" };

        while (pipe.IsConnected && !token.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(token);
            if (line is null)
                break;

            string responseJson;
            try
            {
                using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                requestTimeout.CancelAfter(RequestTimeout);
                responseJson = await DispatchAsync(line).WaitAsync(requestTimeout.Token);
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                responseJson = SerializeError(
                    ReadRequestId(line),
                    "request_timeout",
                    "The editor action did not complete within 5 seconds."
                );
            }
            catch (Exception ex)
            {
                responseJson = SerializeError(ReadRequestId(line), "request_failed", ex.Message);
            }

            await writer.WriteLineAsync(responseJson);
            await writer.FlushAsync(token);
        }
    }

    private static async Task<string> DispatchAsync(string requestJson)
    {
        using var document = JsonDocument.Parse(requestJson);
        var root = document.RootElement;
        var id = root.TryGetProperty("id", out var idElement) ? idElement.GetInt32() : 0;
        var protocolVersion = root.TryGetProperty("protocolVersion", out var versionElement) ?
            versionElement.GetInt32() :
            0;
        if (protocolVersion != ProtocolVersion)
        {
            return SerializeError(
                id,
                "protocol_mismatch",
                $"The editor plugin requires protocol version {ProtocolVersion}, but the client sent version {protocolVersion}."
            );
        }
        var method = root.GetProperty("method").GetString() ?? string.Empty;

        object result = method switch
        {
            "ping" => new { pong = true, utcNow = DateTime.UtcNow },
            "list_actors" => await ListActorsAsync(),
            "screenshot" => await CaptureScreenshotAsync(RequireParam(root, "path")),
            _ => throw new InvalidOperationException($"Unknown method '{method}'"),
        };

        return JsonSerializer.Serialize(new { id, result });
    }

    private static string SerializeError(int id, string code, string message)
    {
        return JsonSerializer.Serialize(new { id, error = new { code, message } });
    }

    private static int ReadRequestId(string requestJson)
    {
        try
        {
            using var document = JsonDocument.Parse(requestJson);
            return document.RootElement.TryGetProperty("id", out var id) ? id.GetInt32() : 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    private static string RequireParam(JsonElement root, string name)
    {
        if (root.TryGetProperty("params", out var paramsElement) &&
            paramsElement.TryGetProperty(name, out var value) &&
            value.GetString() is { } stringValue)
        {
            return stringValue;
        }
        throw new ArgumentException($"Missing required params.{name}");
    }

    private static Task<object> ListActorsAsync()
    {
        var tcs = new TaskCompletionSource<object>();
        Scripting.InvokeOnUpdate(() =>
        {
            try
            {
                var scenes = new List<object>();
                foreach (var scene in Level.Scenes)
                {
                    scenes.Add(new { scene = scene.Name, actors = Walk(scene, 0) });
                }
                tcs.TrySetResult(new { mainThreadId = Globals.MainThreadID, scenes });
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });
        return tcs.Task;
    }

    private static List<object> Walk(Actor actor, int depth)
    {
        var result = new List<object>();
        if (depth > 8)
            return result;

        foreach (var child in actor.Children)
        {
            result.Add(new { name = child.Name, type = child.GetType().Name, children = Walk(child, depth + 1) });
        }
        return result;
    }

    private static async Task<object> CaptureScreenshotAsync(string path)
    {
        var captureQueued = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        Scripting.InvokeOnUpdate(() =>
        {
            try
            {
                if (Engine.IsHeadless)
                {
                    throw new InvalidOperationException(
                        "Screenshot capture is unavailable in headless mode because no rendered viewport output exists.");
                }

                var sceneWindow = FlaxEditor.Editor.Instance.Windows.Windows
                    .OfType<SceneEditorWindow>()
                    .FirstOrDefault(window => window.VisibleInHierarchy);
                var renderTask = sceneWindow?.Viewport.Task;
                if (renderTask?.Output is null)
                {
                    throw new InvalidOperationException(
                        "Screenshot capture requires a visible scene viewport with allocated render output.");
                }

                Screenshot.Capture(renderTask, path);
                captureQueued.TrySetResult(new object());
            }
            catch (Exception ex)
            {
                captureQueued.TrySetException(ex);
            }
        });
        await captureQueued.Task;

        // Capture is queued for the end of the frame by the engine; poll briefly for the file.
        for (var i = 0; i < 20; i++)
        {
            await Task.Delay(100);
            if (File.Exists(path) && new FileInfo(path).Length > 0)
            {
                return new { path, bytes = new FileInfo(path).Length };
            }
        }

        throw new IOException("Screenshot file was not produced within 2 seconds of the viewport capture request.");
    }

    private void WriteHandshakeFile()
    {
        var sessionsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FlaxMcp", "sessions");
        Directory.CreateDirectory(sessionsDir);
        _handshakePath = Path.Combine(sessionsDir, Hash(_projectFolder) + ".json");

        var handshake = new
        {
            pipeName = PipeName,
            protocolVersion = ProtocolVersion,
            pid = Environment.ProcessId,
            projectFolder = _projectFolder,
            pluginVersion = typeof(PipeBridgeServer).Assembly.GetName().Version?.ToString() ?? "0.0.0",
            engineBuild = Globals.EngineBuildNumber,
            startedUtc = DateTime.UtcNow,
        };
        File.WriteAllText(_handshakePath, JsonSerializer.Serialize(handshake));
    }

    private static string Hash(string value)
    {
        var normalized = value.Replace('\\', '/').TrimEnd('/').ToLowerInvariant();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes)[..16];
    }

    public void Dispose()
    {
        _cts.Cancel();
        _currentPipe?.Dispose();

        try
        {
            _acceptLoop?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // Expected: cancellation unwinds through the accept loop.
        }

        _cts.Dispose();

        if (_handshakePath is not null && File.Exists(_handshakePath))
        {
            try { File.Delete(_handshakePath); }
            catch (IOException) { /* best-effort cleanup */ }
        }
    }
}
