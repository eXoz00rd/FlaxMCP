using System.ComponentModel;
using FlaxMcp.Flax;
using FlaxMcp.Flax.Models;
using ModelContextProtocol.Server;

namespace FlaxMcp.Tools;

[McpServerToolType]
public sealed class ContentTools
{
    private readonly FlaxContentIndex _index;

    public ContentTools(FlaxContentIndex index)
    {
        _index = index;
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
}
