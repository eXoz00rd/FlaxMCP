using System.ComponentModel;
using System.Reflection;
using ModelContextProtocol.Server;

namespace FlaxMcp.Tools;

[McpServerToolType]
public sealed class ServerInfoTool
{
    [McpServerTool(Name = "server_info", ReadOnly = true, UseStructuredContent = true)]
    [Description("Returns the FlaxMcp server name and version.")]
    public ServerInfo GetServerInfo()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
        return new ServerInfo("FlaxMcp", version);
    }
}

public sealed record ServerInfo(string Name, string Version);
