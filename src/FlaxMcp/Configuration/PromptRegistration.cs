using FlaxMcp.Prompts;
using FlaxMcp.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace FlaxMcp.Configuration;

public static class PromptRegistration
{
    public static int AddPrompts(IMcpServerBuilder builder, FlaxMcpOptions options)
    {
        var toolTypes = Toolsets.Resolve(options.Toolsets);
        if (options.ReadOnly ||
            !toolTypes.Contains(typeof(BuildTools)) ||
            !toolTypes.Contains(typeof(LogTools)))
        {
            return 0;
        }

        builder.WithPrompts([typeof(WorkflowPrompts)]);
        return 1;
    }
}
