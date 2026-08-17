using System.Text;

namespace FlaxMcp.Configuration;

public static class ServerInstructions
{
    public static string Build(FlaxMcpOptions options, int toolCount)
    {
        var text = new StringBuilder();

        text.AppendLine(
            $"This server inspects a Flax Engine project at '{options.ProjectPath}'. It exposes {toolCount} tools."
        );
        text.AppendLine();

        if (options.ReadOnly)
        {
            text.AppendLine(
                "The server runs in read-only mode: tools that create, update, or delete anything are not available. " +
                "If the user asks for a change, explain that this server is configured for read access only."
            );
            text.AppendLine();
        }

        text.AppendLine("How to use these tools well:");
        text.AppendLine(
            "- project_info reads the .flaxproj file directly: name, version, build targets, referenced " +
            "projects (including the engine and any plugins), and the default scene."
        );

        return text.ToString().TrimEnd();
    }
}
