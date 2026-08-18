using System.Text.Json;
using System.Text.Json.Nodes;
using FlaxMcp.Configuration;
using FlaxMcp.Flax.Models;
using Microsoft.Extensions.Options;

namespace FlaxMcp.Flax;

/// <summary>
/// Indexes every file under a Flax project's Content/ directory, built once and cached for the
/// server process lifetime (rebuilt only on the next server start). Registered as a singleton —
/// tool classes are re-instantiated per call by <see cref="ToolRegistration"/>, so the cache has to
/// live here rather than on a tool instance.
/// </summary>
public sealed class FlaxContentIndex
{
    private static readonly EnumerationOptions ContentEnumerationOptions = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
    };

    private readonly IOptions<FlaxMcpOptions> _options;
    private readonly Lock _lock = new();
    private (IReadOnlyList<FlaxContentAssetInfo> Assets, IReadOnlyDictionary<string, FlaxContentAssetInfo> ById)? _index;

    public FlaxContentIndex(IOptions<FlaxMcpOptions> options)
    {
        _options = options;
    }

    public IReadOnlyList<FlaxContentAssetInfo> Search(string? query, string? typeName, string? extension)
    {
        return GetIndex().Assets
               .Where(asset => query is null || asset.RelativePath.Contains(query, StringComparison.OrdinalIgnoreCase))
               .Where(asset => typeName is null || string.Equals(asset.TypeName, typeName, StringComparison.OrdinalIgnoreCase))
               .Where(asset => extension is null || string.Equals(asset.Extension, extension, StringComparison.OrdinalIgnoreCase))
               .Take(ResponseLimits.DefaultListTop)
               .ToArray();
    }

    public FlaxContentAssetInfo? GetById(string id)
    {
        return GetIndex().ById.GetValueOrDefault(id);
    }

    private (IReadOnlyList<FlaxContentAssetInfo> Assets, IReadOnlyDictionary<string, FlaxContentAssetInfo> ById) GetIndex()
    {
        lock (_lock)
        {
            return _index ??= BuildIndex();
        }
    }

    private (IReadOnlyList<FlaxContentAssetInfo>, IReadOnlyDictionary<string, FlaxContentAssetInfo>) BuildIndex()
    {
        var contentDirectory = _options.Value.ResolveContentDirectory();

        if (!Directory.Exists(contentDirectory))
        {
            return ([], new Dictionary<string, FlaxContentAssetInfo>());
        }

        var assets = Directory
                     .EnumerateFiles(contentDirectory, "*", ContentEnumerationOptions)
                     .Select(filePath => ReadAsset(filePath, contentDirectory))
                     .ToList();

        var byId = new Dictionary<string, FlaxContentAssetInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var asset in assets)
        {
            if (asset.Id is not null)
            {
                byId.TryAdd(asset.Id, asset);
            }
        }

        return (assets, byId);
    }

    private static FlaxContentAssetInfo ReadAsset(string filePath, string contentDirectory)
    {
        var extension = Path.GetExtension(filePath);
        var relativePath = Path.GetRelativePath(contentDirectory, filePath).Replace('\\', '/');

        var metadata = extension.ToLowerInvariant() switch
        {
            ".flax" => FlaxBinaryAssetHeaderReader.TryRead(filePath),
            ".json" or ".scene" or ".prefab" => TryReadJsonEnvelope(filePath),
            _ => null,
        };

        return new FlaxContentAssetInfo(metadata?.Id, metadata?.TypeName, relativePath, extension);
    }

    private static (string Id, string TypeName)? TryReadJsonEnvelope(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            if (JsonNode.Parse(stream) is not JsonObject document)
            {
                return null;
            }

            var id = document["ID"]?.GetValue<string>();
            return id is null ? null : (id, document["TypeName"]?.GetValue<string>() ?? string.Empty);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return null;
        }
    }
}
