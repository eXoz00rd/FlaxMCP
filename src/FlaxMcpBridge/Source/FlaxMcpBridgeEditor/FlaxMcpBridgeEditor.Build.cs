using Flax.Build;
using Flax.Build.NativeCpp;

public class FlaxMcpBridgeEditor : GameEditorModule
{
    /// <inheritdoc />
    public override void Setup(BuildOptions options)
    {
        base.Setup(options);

        options.PublicDependencies.Add("FlaxMcpBridge");
        options.ScriptingAPI.SystemReferences.Add("System.IO.Pipes");
        options.ScriptingAPI.SystemReferences.Add("System.Text.Json");
        options.ScriptingAPI.SystemReferences.Add("System.Text.Encoding.Extensions");

        BuildNativeCode = false;
    }
}
