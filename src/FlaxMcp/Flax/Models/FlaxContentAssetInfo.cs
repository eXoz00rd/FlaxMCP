namespace FlaxMcp.Flax.Models;

public sealed record FlaxContentAssetInfo(
    string? Id,
    string? TypeName,
    string RelativePath,
    string Extension
);
