using System;
using FlaxEngine;

namespace FlaxMcpBridge;

/// <summary>
/// Runtime placeholder plugin. The bridge itself only runs inside the Editor
/// (FlaxMcpBridge.Editor.BridgeEditorPlugin); this class exists because
/// <c>EditorPlugin.GamePluginType</c> expects a paired game plugin.
/// </summary>
public class BridgeGamePlugin : GamePlugin
{
    /// <inheritdoc />
    public BridgeGamePlugin()
    {
        _description = new PluginDescription
        {
            Name = "FlaxMcp Bridge",
            Category = "Other",
            Author = "FlaxMcp",
            RepositoryUrl = "https://github.com/eXoz00rd/FlaxMCP",
            Description = "Runtime counterpart of the FlaxMcp editor bridge plugin.",
            Version = new Version(0, 1, 0),
            IsAlpha = true,
            IsBeta = false,
        };
    }
}
