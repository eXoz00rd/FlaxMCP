using FlaxMcp.Tools;
using Xunit;

namespace FlaxMcp.Tests.Tools;

public class ServerInfoToolTests
{
    [Fact]
    public void GetServerInfo_ReturnsFlaxMcpName()
    {
        var tool = new ServerInfoTool();

        var info = tool.GetServerInfo();

        Assert.Equal("FlaxMcp", info.Name);
        Assert.False(string.IsNullOrWhiteSpace(info.Version));
    }
}
