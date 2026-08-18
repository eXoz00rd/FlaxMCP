using Flax.Build;

public class FlaxMcpBridgeEditorTarget : GameProjectEditorTarget
{
    /// <inheritdoc />
    public override void Init()
    {
        base.Init();

        Modules.Add("FlaxMcpBridge");
        Modules.Add("FlaxMcpBridgeEditor");
    }
}
