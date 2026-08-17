using Flax.Build;

public class FlaxMcpBridgeTarget : GameProjectTarget
{
    /// <inheritdoc />
    public override void Init()
    {
        base.Init();

        Modules.Add("FlaxMcpBridge");
    }
}
