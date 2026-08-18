using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FlaxMcp.Flax;

/// <summary>
/// Refuses to start a second headless <c>FlaxEditor.exe</c> run against a project that already has
/// a live session, by sharing the handshake-file convention the live-editor bridge already writes
/// (<c>%APPDATA%/FlaxMcp/sessions/&lt;hash16&gt;.json</c>, see
/// <c>FlaxMcpBridge.Editor.PipeBridgeServer.WriteHandshakeFile</c>). A live bridge session (an open
/// editor GUI with the plugin loaded) and a headless run recognize each other through the same file
/// and hash, without either depending on the other's presence.
/// </summary>
public sealed class FlaxEditorSessionGuard
{
    private readonly string _sessionsDirectory;

    public FlaxEditorSessionGuard(string? sessionsDirectory = null)
    {
        _sessionsDirectory = sessionsDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FlaxMcp", "sessions");
    }

    /// <summary>
    /// Acquires exclusive use of <paramref name="projectFolder"/> for the caller's process, or throws
    /// if another live session already holds it. Dispose the returned lease to release it.
    /// </summary>
    public FlaxEditorSessionLease Acquire(string projectFolder)
    {
        Directory.CreateDirectory(_sessionsDirectory);
        var handshakePath = Path.Combine(_sessionsDirectory, Hash(projectFolder) + ".json");

        if (TryReadLiveSession(handshakePath, out var pid, out var startedUtc))
        {
            throw new InvalidOperationException(
                $"'{projectFolder}' already has a live Flax Editor session (pid {pid}, started {startedUtc:O} UTC). " +
                "Close it, or wait for it to finish, before launching another headless run."
            );
        }

        File.WriteAllText(
            handshakePath,
            JsonSerializer.Serialize(new { pid = Environment.ProcessId, projectFolder, startedUtc = DateTime.UtcNow })
        );
        return new FlaxEditorSessionLease(handshakePath);
    }

    private static bool TryReadLiveSession(string handshakePath, out int pid, out DateTime startedUtc)
    {
        pid = 0;
        startedUtc = default;

        if (!File.Exists(handshakePath))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(handshakePath));
            var root = document.RootElement;
            pid = root.GetProperty("pid").GetInt32();
            startedUtc = root.TryGetProperty("startedUtc", out var started) ? started.GetDateTime() : DateTime.UtcNow;
        }
        catch (Exception ex) when (ex is JsonException or IOException or KeyNotFoundException or FormatException)
        {
            // Unreadable/malformed handshake file, most likely left over from a crash. Treat as stale.
            TryDelete(handshakePath);
            return false;
        }

        if (IsProcessAlive(pid))
        {
            return true;
        }

        TryDelete(handshakePath);
        return false;
    }

    private static bool IsProcessAlive(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Best-effort cleanup; a concurrent acquirer may have already replaced it.
        }
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value.ToLowerInvariant()));
        return Convert.ToHexString(bytes)[..16];
    }
}

/// <summary>
/// Holds a <see cref="FlaxEditorSessionGuard"/> lock. Dispose it (e.g. via <c>using</c>) as soon as
/// the headless run finishes, so the next launch doesn't see a stale-but-still-alive-process false
/// positive for the current process.
/// </summary>
public sealed class FlaxEditorSessionLease : IDisposable
{
    private readonly string _handshakePath;

    internal FlaxEditorSessionLease(string handshakePath)
    {
        _handshakePath = handshakePath;
    }

    public void Dispose()
    {
        try
        {
            File.Delete(_handshakePath);
        }
        catch (IOException)
        {
            // Best-effort cleanup, mirrors PipeBridgeServer.Dispose.
        }
    }
}
