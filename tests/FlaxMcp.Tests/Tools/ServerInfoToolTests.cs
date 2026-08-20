using FlaxMcp.Flax;
using FlaxMcp.Tools;
using Xunit;

namespace FlaxMcp.Tests.Tools;

public class ServerInfoToolTests
{
    [Fact]
    public void GetServerInfo_ReturnsFlaxMcpName()
    {
        var tool = new ServerInfoTool(new StubBridgeClient(FlaxBridgeStatus.Disconnected));

        var info = tool.GetServerInfo();

        Assert.Equal("FlaxMcp", info.Name);
        Assert.False(string.IsNullOrWhiteSpace(info.Version));
    }

    [Fact]
    public async Task GetFlaxStatusAsync_ReturnsBridgeClientStatus()
    {
        var expected = new FlaxBridgeStatus(true, "1.2.3", 12000, DateTime.UtcNow);
        var tool = new ServerInfoTool(new StubBridgeClient(expected));

        var status = await tool.GetFlaxStatusAsync(TestContext.Current.CancellationToken);

        Assert.Equal(expected, status);
    }

    private sealed class StubBridgeClient(FlaxBridgeStatus status) : IFlaxBridgeClient
    {
        public Task<FlaxBridgeStatus> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(status);
        }

        public Task<FlaxBridgePing> PingAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<FlaxLiveSceneGraph> GetSceneGraphAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<FlaxBridgeScreenshot> CaptureScreenshotAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
