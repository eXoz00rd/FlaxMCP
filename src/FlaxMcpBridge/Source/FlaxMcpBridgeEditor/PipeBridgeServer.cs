using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FlaxEngine;
using FlaxEditor.Windows;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace FlaxMcpBridge.Editor;

/// <summary>
/// Named-pipe bridge transport accepting one client connection at a time and exchanging
/// newline-delimited JSON messages.
/// </summary>
internal sealed class PipeBridgeServer : IDisposable
{
    private const int ProtocolVersion = 8;
    private const int MaxSceneGraphDepth = 32;
    private const int MaxSceneGraphNodes = 500;
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
                var dispatchTask = DispatchAsync(line);
                try
                {
                    responseJson = await dispatchTask.WaitAsync(requestTimeout.Token);
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                {
                    ObserveFault(dispatchTask);
                    throw;
                }
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                responseJson = SerializeError(
                    ReadRequestId(line),
                    "request_timeout",
                    $"The editor action did not complete within {RequestTimeout.TotalSeconds:0} seconds."
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
        var id = 0;
        if (root.TryGetProperty("id", out var idElement) && !idElement.TryGetInt32(out id))
        {
            return SerializeError(0, "invalid_request", "Request id must be a 32-bit integer.");
        }

        var protocolVersion = 0;
        if (root.TryGetProperty("protocolVersion", out var versionElement) &&
            !versionElement.TryGetInt32(out protocolVersion))
        {
            return SerializeError(id, "invalid_request", "Protocol version must be a 32-bit integer.");
        }

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
            "scene_graph" => await GetSceneGraphAsync(),
            "get_selection" => await GetSelectionAsync(),
            "set_selection" => await SetSelectionAsync(RequireStringArrayParam(root, "actorIds")),
            "actor_details" => await GetActorDetailsAsync(RequireParam(root, "actorId")),
            "modify_actor" => await ModifyActorAsync(
                RequireParam(root, "actorId"),
                RequireObjectParam(root, "transform")
            ),
            "create_actor" => await CreateActorAsync(
                RequireParam(root, "actorType"), RequireParam(root, "name"),
                OptionalParam(root, "sceneId"), OptionalParam(root, "parentId"),
                RequireObjectParam(root, "transform")
            ),
            "duplicate_actor" => await DuplicateActorAsync(
                RequireParam(root, "actorId"), RequireParam(root, "name"),
                OptionalParam(root, "sceneId"), OptionalParam(root, "parentId"),
                RequireObjectParam(root, "transform")
            ),
            "rename_actor" => await RenameActorAsync(
                RequireParam(root, "actorId"), RequireParam(root, "name")
            ),
            "reparent_actor" => await ReparentActorAsync(
                RequireParam(root, "actorId"), OptionalParam(root, "sceneId"),
                OptionalParam(root, "parentId"), RequireBoolParam(root, "preserveWorldTransform")
            ),
            "save" => await SaveAsync(),
            "play_mode" => await SetPlayModeAsync(RequireParam(root, "action")),
            "screenshot" => await CaptureScreenshotAsync(RequireParam(root, "path")),
            "execute_csharp" => await ExecuteCSharpAsync(RequireParam(root, "code")),
            _ => throw new InvalidOperationException($"Unknown method '{method}'"),
        };

        return JsonSerializer.Serialize(new { id, result });
    }

    private static string SerializeError(int id, string code, string message)
    {
        return JsonSerializer.Serialize(new { id, error = new { code, message } });
    }

    private static void ObserveFault(Task task)
    {
        _ = task.ContinueWith(
            completedTask => _ = completedTask.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default
        );
    }

    private static int ReadRequestId(string requestJson)
    {
        try
        {
            using var document = JsonDocument.Parse(requestJson);
            return document.RootElement.TryGetProperty("id", out var id) && id.TryGetInt32(out var value) ?
                value :
                0;
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

    private static string? OptionalParam(JsonElement root, string name)
    {
        return root.TryGetProperty("params", out var paramsElement) &&
               paramsElement.TryGetProperty(name, out var value) &&
               value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static IReadOnlyList<string> RequireStringArrayParam(JsonElement root, string name)
    {
        if (!root.TryGetProperty("params", out var paramsElement) ||
            !paramsElement.TryGetProperty(name, out var value) ||
            value.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException($"Missing required params.{name} array");
        }

        var result = new List<string>(value.GetArrayLength());
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException($"params.{name} must contain strings");
            }

            result.Add(item.GetString()!);
        }
        return result;
    }

    private static JsonElement RequireObjectParam(JsonElement root, string name)
    {
        if (root.TryGetProperty("params", out var paramsElement) &&
            paramsElement.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.Object)
        {
            return value.Clone();
        }

        throw new ArgumentException($"Missing required params.{name} object");
    }

    private static bool RequireBoolParam(JsonElement root, string name)
    {
        if (root.TryGetProperty("params", out var paramsElement) &&
            paramsElement.TryGetProperty(name, out var value) &&
            (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False))
        {
            return value.GetBoolean();
        }

        throw new ArgumentException($"Missing required params.{name} boolean");
    }

    private static Task<object> GetSceneGraphAsync()
    {
        var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        Scripting.InvokeOnUpdate(() =>
        {
            try
            {
                var state = new SceneGraphState();
                var scenes = new List<object>();
                foreach (var scene in Level.Scenes)
                {
                    if (state.NodeCount >= MaxSceneGraphNodes)
                    {
                        state.Truncated = true;
                        break;
                    }

                    scenes.Add(BuildSceneNode(scene, 0, state));
                }
                tcs.TrySetResult(new { mainThreadId = Globals.MainThreadID, scenes, truncated = state.Truncated });
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });
        return tcs.Task;
    }

    private static Task<object> GetSelectionAsync()
    {
        return InvokeOnUpdateAsync(GetSelection);
    }

    private static Task<object> SetSelectionAsync(IReadOnlyList<string> actorIds)
    {
        return InvokeOnUpdateAsync(() =>
        {
            var nodes = new List<FlaxEditor.SceneGraph.SceneGraphNode>(actorIds.Count);
            foreach (var actorId in actorIds)
            {
                if (!Guid.TryParse(actorId, out var id))
                {
                    throw new ArgumentException($"Actor id '{actorId}' is not a valid GUID.");
                }

                var node = FlaxEditor.Editor.Instance.Scene.GetActorNode(id) ??
                    throw new KeyNotFoundException($"Actor '{actorId}' is not loaded in the editor scene graph.");
                nodes.Add(node);
            }

            FlaxEditor.Editor.Instance.SceneEditing.Select(nodes, additive: false);
            return GetSelection();
        });
    }

    private static object GetSelection()
    {
        var selected = FlaxEditor.Editor.Instance.SceneEditing.Selection
            .OfType<FlaxEditor.SceneGraph.ActorNode>()
            .Select(node => new
            {
                id = node.Actor.ID.ToString("D"),
                typeName = node.Actor.GetType().FullName ?? node.Actor.GetType().Name,
                name = node.Actor.Name,
            })
            .ToList();
        return new { mainThreadId = Globals.MainThreadID, selected };
    }

    private static Task<object> GetActorDetailsAsync(string actorId)
    {
        return InvokeOnUpdateAsync(() =>
        {
            if (!Guid.TryParse(actorId, out var id))
            {
                throw new ArgumentException($"Actor id '{actorId}' is not a valid GUID.");
            }

            var actor = Level.FindActor(id) ??
                throw new KeyNotFoundException($"Actor '{actorId}' is not loaded in the editor.");
            return BuildActorDetails(actor);
        });
    }

    private static Task<object> ModifyActorAsync(string actorId, JsonElement transform)
    {
        return InvokeOnUpdateAsync(() =>
        {
            if (!Guid.TryParse(actorId, out var id))
            {
                throw new ArgumentException($"Actor id '{actorId}' is not a valid GUID.");
            }

            var actor = Level.FindActor(id) ??
                throw new KeyNotFoundException($"Actor '{actorId}' is not loaded in the editor.");
            actor.Transform = ReadTransform(transform);
            if (actor.Scene is { } scene)
            {
                FlaxEditor.Editor.Instance.Scene.MarkSceneEdited(scene);
            }
            return BuildActorDetails(actor);
        });
    }

    private static Task<object> CreateActorAsync(
        string actorType, string name, string? sceneId, string? parentId, JsonElement transform)
    {
        return InvokeOnUpdateAsync(() =>
        {
            ValidateActorName(name);
            var destination = ResolveDestination(sceneId, parentId);
            var actorTransform = ReadTransform(transform);
            Actor actor = actorType switch
            {
                "EmptyActor" => new EmptyActor(),
                "StaticModel" => new StaticModel(),
                _ => throw new ArgumentException(
                    $"Actor type '{actorType}' is not allowed. Allowed types: EmptyActor, StaticModel."
                ),
            };
            return SpawnActor(actor, name, destination, actorTransform);
        });
    }

    private static Task<object> DuplicateActorAsync(
        string actorId, string name, string? sceneId, string? parentId, JsonElement transform)
    {
        return InvokeOnUpdateAsync(() =>
        {
            ValidateActorName(name);
            var destination = ResolveDestination(sceneId, parentId);
            var actorTransform = ReadTransform(transform);
            if (!Guid.TryParse(actorId, out var id))
            {
                throw new ArgumentException($"Actor id '{actorId}' is not a valid GUID.");
            }
            var source = Level.FindActor(id) ??
                throw new KeyNotFoundException($"Actor '{actorId}' is not loaded in the editor.");
            return SpawnActor(source.Clone(), name, destination, actorTransform);
        });
    }

    private static Task<object> RenameActorAsync(string actorId, string name)
    {
        return InvokeOnUpdateAsync(() =>
        {
            ValidateActorName(name);
            var actor = FindActor(actorId);
            FlaxEditor.Editor.Instance.SceneEditing.Undo.RecordAction(actor, "Rename actor", () =>
            {
                actor.Name = name;
                MarkSceneEdited(actor);
            });
            return BuildActorDetails(actor);
        });
    }

    private static Task<object> ReparentActorAsync(
        string actorId, string? sceneId, string? parentId, bool preserveWorldTransform)
    {
        return InvokeOnUpdateAsync(() =>
        {
            var actor = FindActor(actorId);
            var destination = ResolveDestination(sceneId, parentId);
            if (actor.Scene != destination.Scene)
            {
                throw new InvalidOperationException("Moving actors between scenes is not supported.");
            }

            for (Actor? ancestor = destination; ancestor is not null; ancestor = ancestor.Parent)
            {
                if (ancestor.ID == actor.ID)
                {
                    throw new InvalidOperationException("An actor cannot be parented to itself or its descendant.");
                }
            }

            FlaxEditor.Editor.Instance.SceneEditing.Undo.RecordAction(actor, "Reparent actor", () =>
            {
                actor.SetParent(destination, preserveWorldTransform, false);
                MarkSceneEdited(actor);
            });
            return BuildActorDetails(actor);
        });
    }

    private static Actor FindActor(string actorId)
    {
        if (!Guid.TryParse(actorId, out var id))
        {
            throw new ArgumentException($"Actor id '{actorId}' is not a valid GUID.");
        }

        return Level.FindActor(id) ??
            throw new KeyNotFoundException($"Actor '{actorId}' is not loaded in the editor.");
    }

    private static void MarkSceneEdited(Actor actor)
    {
        if (actor.Scene is { } scene)
        {
            FlaxEditor.Editor.Instance.Scene.MarkSceneEdited(scene);
        }
    }

    private static void ValidateActorName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Actor name cannot be empty.");
        }
    }

    private static Actor ResolveDestination(string? sceneId, string? parentId)
    {
        if ((sceneId is null) == (parentId is null))
        {
            throw new ArgumentException("Set exactly one of sceneId or parentId.");
        }

        Actor destination;
        if (parentId is not null)
        {
            if (!Guid.TryParse(parentId, out var id))
            {
                throw new ArgumentException($"Parent actor id '{parentId}' is not a valid GUID.");
            }
            destination = Level.FindActor(id) ??
                throw new KeyNotFoundException($"Parent actor '{parentId}' is not loaded in the editor.");
        }
        else
        {
            if (!Guid.TryParse(sceneId, out var id))
            {
                throw new ArgumentException($"Scene id '{sceneId}' is not a valid GUID.");
            }
            destination = Level.Scenes.FirstOrDefault(scene => scene.ID == id) ??
                throw new KeyNotFoundException($"Scene '{sceneId}' is not loaded in the editor.");
        }

        return destination;
    }

    private static object SpawnActor(Actor actor, string name, Actor destination, Transform transform)
    {
        actor.Name = name;
        actor.Transform = transform;
        FlaxEditor.Editor.Instance.SceneEditing.Spawn(actor, destination, -1, false);
        return BuildActorDetails(actor);
    }

    private static Transform ReadTransform(JsonElement value)
    {
        var translation = value.GetProperty("translation");
        var orientation = value.GetProperty("orientation");
        var scale = value.GetProperty("scale");
        return new Transform(
            new Vector3(
                translation.GetProperty("x").GetSingle(),
                translation.GetProperty("y").GetSingle(),
                translation.GetProperty("z").GetSingle()
            ),
            new Quaternion(
                orientation.GetProperty("x").GetSingle(),
                orientation.GetProperty("y").GetSingle(),
                orientation.GetProperty("z").GetSingle(),
                orientation.GetProperty("w").GetSingle()
            ),
            new Vector3(
                scale.GetProperty("x").GetSingle(),
                scale.GetProperty("y").GetSingle(),
                scale.GetProperty("z").GetSingle()
            )
        );
    }

    private static object BuildActorDetails(Actor actor)
    {
        return new
        {
            mainThreadId = Globals.MainThreadID,
            id = actor.ID.ToString("D"),
            typeName = actor.GetType().FullName ?? actor.GetType().Name,
            name = actor.Name,
            parentId = actor.Parent?.ID.ToString("D"),
            sceneId = actor.Scene?.ID.ToString("D"),
            isActive = actor.IsActive,
            isActiveInHierarchy = actor.IsActiveInHierarchy,
            layer = actor.Layer,
            layerName = actor.LayerName,
            tags = actor.Tags.Select(tag => tag.ToString()).ToList(),
            transform = BuildTransform(actor.Transform),
            localTransform = BuildTransform(actor.LocalTransform),
            scripts = actor.Scripts.Select(script => new
            {
                id = script.ID.ToString("D"),
                typeName = script.GetType().FullName ?? script.GetType().Name,
                enabled = script.Enabled,
                isEnabledInHierarchy = script.IsEnabledInHierarchy,
            }).ToList(),
        };
    }

    private static object BuildTransform(Transform transform)
    {
        return new
        {
            translation = new
            {
                x = transform.Translation.X,
                y = transform.Translation.Y,
                z = transform.Translation.Z,
            },
            orientation = new
            {
                x = transform.Orientation.X,
                y = transform.Orientation.Y,
                z = transform.Orientation.Z,
                w = transform.Orientation.W,
            },
            scale = new
            {
                x = transform.Scale.X,
                y = transform.Scale.Y,
                z = transform.Scale.Z,
            },
        };
    }

    private static Task<object> InvokeOnUpdateAsync(Func<object> action)
    {
        var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        Scripting.InvokeOnUpdate(() =>
        {
            try
            {
                tcs.TrySetResult(action());
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });
        return tcs.Task;
    }

    private static object BuildSceneNode(Actor actor, int depth, SceneGraphState state)
    {
        state.NodeCount++;
        var children = new List<object>();

        if (actor.Children.Any() && depth >= MaxSceneGraphDepth)
        {
            state.Truncated = true;
        }
        else
        {
            foreach (var child in actor.Children)
            {
                if (state.NodeCount >= MaxSceneGraphNodes)
                {
                    state.Truncated = true;
                    break;
                }

                children.Add(BuildSceneNode(child, depth + 1, state));
            }
        }

        return new
        {
            id = actor.ID.ToString(),
            typeName = actor.GetType().FullName ?? actor.GetType().Name,
            name = actor.Name,
            children,
        };
    }

    private sealed class SceneGraphState
    {
        public int NodeCount;
        public bool Truncated;
    }

    private sealed record PlayModeState(int MainThreadId, bool IsPlayMode, bool IsPaused)
    {
        public PlayModeState(bool isPlayMode, bool isPaused)
            : this(0, isPlayMode, isPaused)
        {
        }
    }

    private static Task<object> SaveAsync()
    {
        return InvokeOnUpdateAsync(() =>
        {
            FlaxEditor.Editor.Instance.SaveAll();
            return new { mainThreadId = Globals.MainThreadID, saved = true };
        });
    }

    private static async Task<object> SetPlayModeAsync(string action)
    {
        var normalizedAction = action.ToLowerInvariant();
        var expectedState = normalizedAction switch
        {
            "start" => new PlayModeState(true, false),
            "stop" => new PlayModeState(false, false),
            "pause" => new PlayModeState(true, true),
            "resume" => new PlayModeState(true, false),
            _ => throw new ArgumentException("Play mode action must be start, stop, pause, or resume."),
        };

        await InvokeOnUpdateAsync(() =>
        {
            var editor = FlaxEditor.Editor.Instance;
            switch (normalizedAction)
            {
                case "start":
                    editor.Simulation.RequestStartPlayScenes();
                    break;
                case "stop":
                    editor.Simulation.RequestStopPlay();
                    break;
                case "pause":
                    editor.Simulation.RequestPausePlay();
                    break;
                case "resume":
                    editor.Simulation.RequestResumePlay();
                    break;
            }

            return new object();
        });

        for (var attempt = 0; attempt < 40; attempt++)
        {
            var state = await ReadPlayModeStateAsync();
            if (state.IsPlayMode == expectedState.IsPlayMode && state.IsPaused == expectedState.IsPaused)
            {
                return new
                {
                    mainThreadId = state.MainThreadId,
                    requestedAction = normalizedAction,
                    isPlayMode = state.IsPlayMode,
                    isPaused = state.IsPaused,
                };
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"The Flax Editor did not complete the '{normalizedAction}' play mode action.");
    }

    private static Task<PlayModeState> ReadPlayModeStateAsync()
    {
        var tcs = new TaskCompletionSource<PlayModeState>(TaskCreationOptions.RunContinuationsAsynchronously);
        Scripting.InvokeOnUpdate(() =>
        {
            var editor = FlaxEditor.Editor.Instance;
            var playingState = editor.StateMachine.CurrentState as FlaxEditor.States.PlayingState;
            tcs.TrySetResult(new PlayModeState(
                checked((int)Globals.MainThreadID),
                FlaxEditor.Editor.IsPlayMode,
                playingState?.IsPaused ?? false
            ));
        });
        return tcs.Task;
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
                    .OfType<EditGameWindow>()
                    .FirstOrDefault(window => window.IsSelected && window.VisibleInHierarchy && !window.IsHidden);
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

    private static async Task<object> ExecuteCSharpAsync(string code)
    {
        const string typeName = "FlaxMcpBridge.Dynamic.CodeExecution";
        var source = $$"""
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using FlaxEngine;
            using FlaxEditor;

            namespace FlaxMcpBridge.Dynamic;

            public static class CodeExecution
            {
                public static object? Execute()
                {
                    {{code}}
                }
            }
            """;
        var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            .ToArray();
        var references = loadedAssemblies
            .Select(assembly => assembly.Location)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create(
            "FlaxMcpDynamic_" + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        using var assemblyStream = new MemoryStream();
        var emitResult = compilation.Emit(assemblyStream);
        if (!emitResult.Success)
        {
            var errors = emitResult.Diagnostics
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .Select(diagnostic => diagnostic.ToString());
            throw new InvalidOperationException("C# compilation failed: " + string.Join(Environment.NewLine, errors));
        }

        assemblyStream.Position = 0;
        var loadContext = new AssemblyLoadContext(compilation.AssemblyName, isCollectible: true);
        loadContext.Resolving += (_, assemblyName) => loadedAssemblies.FirstOrDefault(
            assembly => AssemblyName.ReferenceMatchesDefinition(assembly.GetName(), assemblyName)
        );
        try
        {
            var assembly = loadContext.LoadFromStream(assemblyStream);
            var method = assembly.GetType(typeName)?.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static) ??
                throw new InvalidOperationException("Compiled C# entry point was not found.");
            return await InvokeOnUpdateAsync(() =>
            {
                try
                {
                    var result = method.Invoke(null, null);
                    return new
                    {
                        mainThreadId = checked((int)Globals.MainThreadID),
                        typeName = result?.GetType().FullName,
                        result = result is null ?
                            (JsonElement?)null :
                            JsonSerializer.SerializeToElement(result, result.GetType()),
                    };
                }
                catch (TargetInvocationException ex) when (ex.InnerException is not null)
                {
                    throw ex.InnerException;
                }
            });
        }
        finally
        {
            loadContext.Unload();
        }
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
