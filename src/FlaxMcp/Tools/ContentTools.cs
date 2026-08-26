using System.ComponentModel;
using FlaxMcp.Flax;
using FlaxMcp.Flax.Models;
using ModelContextProtocol.Server;

namespace FlaxMcp.Tools;

[McpServerToolType]
public sealed class ContentTools
{
    private readonly FlaxContentIndex _index;
    private readonly IFlaxBridgeClient _bridgeClient;

    public ContentTools(FlaxContentIndex index, IFlaxBridgeClient bridgeClient)
    {
        _index = index;
        _bridgeClient = bridgeClient;
    }

    [McpServerTool(Name = "content_search", ReadOnly = true, UseStructuredContent = true)]
    [Description("Searches the Content/ index by partial path match, TypeName, and/or extension. Id/TypeName are null for files whose format isn't recognized or a .flax header that doesn't match the reverse-engineered layout (verified against Flax 1.12).")]
    public IReadOnlyList<FlaxContentAssetInfo> SearchContent(string? query = null, string? typeName = null, string? extension = null)
    {
        return _index.Search(query, typeName, extension);
    }

    [McpServerTool(Name = "content_asset_info", ReadOnly = true, UseStructuredContent = true)]
    [Description("Returns metadata (Id, TypeName, RelativePath, Extension) for the content asset with the given GUID.")]
    public FlaxContentAssetInfo GetAssetInfo(string id)
    {
        return _index.GetById(id) ?? throw new InvalidOperationException($"No content asset found with ID '{id}'.");
    }

    [McpServerTool(Name = "content_resolve_guid", ReadOnly = true, UseStructuredContent = true)]
    [Description("Resolves a content asset GUID to its path relative to Content/.")]
    public string ResolveGuid(string id)
    {
        var asset = _index.GetById(id) ?? throw new InvalidOperationException($"No content asset found with ID '{id}'.");
        return asset.RelativePath;
    }

    [McpServerTool(Name = "content_create_material", UseStructuredContent = true)]
    [Description("Creates a surface Material asset through the live Flax Editor. Paths are relative to Content/ and must end in .flax.")]
    public Task<FlaxMaterialDetails> CreateMaterialAsync(
        [Description("Destination path relative to Content/, ending in .flax.")] string relativePath,
        [Description("Uniform RGBA base color, with every component in the 0-1 range.")] FlaxColor baseColor,
        [Description("Surface roughness in the 0-1 range.")] double roughness,
        [Description("Surface metallic value in the 0-1 range.")] double metallic,
        [Description("Optional emissive RGBA color, with every component in the 0-1 range.")] FlaxColor? emissiveColor = null,
        [Description("Optional FlaxEngine.Texture GUID used for base color.")] string? baseColorTextureId = null,
        [Description("Optional normal-map FlaxEngine.Texture GUID.")] string? normalTextureId = null,
        [Description("Optional positive UV tiling applied to supplied textures.")] FlaxVector2? uvTiling = null,
        CancellationToken cancellationToken = default)
    {
        return _bridgeClient.CreateMaterialAsync(relativePath, baseColor, roughness, metallic,
            emissiveColor, baseColorTextureId, normalTextureId, uvTiling, cancellationToken);
    }

    [McpServerTool(Name = "content_material_details", ReadOnly = true, UseStructuredContent = true)]
    [Description("Returns typed values, texture references, UV tiling, and exposed parameters for a live surface Material asset.")]
    public Task<FlaxMaterialDetails> GetMaterialDetailsAsync(
        [Description("FlaxEngine.Material asset GUID from content_search.")] string materialId,
        CancellationToken cancellationToken = default)
    {
        return _bridgeClient.GetMaterialDetailsAsync(materialId, cancellationToken);
    }
}
