using FlaxMcp.Flax;
using Xunit;

namespace FlaxMcp.Tests.Flax;

public sealed class FlaxEditorSessionGuardTests : IDisposable
{
    // Overwhelmingly unlikely to be a real, running process ID on any supported platform -- used to
    // exercise the stale-handshake path deterministically, without spawning and waiting on a real process.
    private const int DefinitelyDeadPid = 999_999_999;

    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "FlaxMcpTests_" + Guid.NewGuid().ToString("N"));
    private readonly FlaxEditorSessionGuard _guard;

    public FlaxEditorSessionGuardTests()
    {
        Directory.CreateDirectory(_tempDir);
        _guard = new FlaxEditorSessionGuard(_tempDir);
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Acquire_WithNoExistingSession_Succeeds()
    {
        using var lease = _guard.Acquire(@"D:\SomeProject");

        Assert.NotNull(lease);
    }

    [Fact]
    public void Acquire_WhileAnotherSessionIsLiveForTheSameProject_ThrowsWithClearMessage()
    {
        using var firstLease = _guard.Acquire(@"D:\SomeProject");

        var exception = Assert.Throws<InvalidOperationException>(() => _guard.Acquire(@"D:\SomeProject"));

        Assert.Contains(@"D:\SomeProject", exception.Message);
        Assert.Contains(Environment.ProcessId.ToString(), exception.Message);
    }

    [Fact]
    public void Acquire_ForADifferentProject_DoesNotConflict()
    {
        using var firstLease = _guard.Acquire(@"D:\ProjectA");

        using var secondLease = _guard.Acquire(@"D:\ProjectB");

        Assert.NotNull(secondLease);
    }

    [Fact]
    public void Acquire_AfterLeaseIsDisposed_SucceedsAgain()
    {
        var lease = _guard.Acquire(@"D:\SomeProject");
        lease.Dispose();

        using var secondLease = _guard.Acquire(@"D:\SomeProject");

        Assert.NotNull(secondLease);
    }

    [Fact]
    public void Acquire_WithStaleHandshakeFromADeadProcess_TreatsItAsAvailableAndSucceeds()
    {
        var handshakePath = Path.Combine(_tempDir, ProjectHash(@"D:\SomeProject") + ".json");
        File.WriteAllText(handshakePath, $$"""{"pid":{{DefinitelyDeadPid}},"projectFolder":"D:\\SomeProject","startedUtc":"2020-01-01T00:00:00Z"}""");

        using var lease = _guard.Acquire(@"D:\SomeProject");

        Assert.NotNull(lease);
    }

    [Fact]
    public void Acquire_WithMalformedHandshakeFile_TreatsItAsStaleAndSucceeds()
    {
        var handshakePath = Path.Combine(_tempDir, ProjectHash(@"D:\SomeProject") + ".json");
        File.WriteAllText(handshakePath, "{ not valid json");

        using var lease = _guard.Acquire(@"D:\SomeProject");

        Assert.NotNull(lease);
    }

    private static string ProjectHash(string projectFolder)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(projectFolder.ToLowerInvariant()));
        return Convert.ToHexString(bytes)[..16];
    }
}
