using Flax.Build;
using Flax.Build.NativeCpp;

public class FlaxMcpBridge : GameModule
{
    /// <inheritdoc />
    public override void Setup(BuildOptions options)
    {
        base.Setup(options);

        BuildNativeCode = false;
    }
}
