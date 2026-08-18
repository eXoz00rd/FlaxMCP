using System.Text.Json;
using System.Text.Json.Nodes;
using FlaxMcp.Configuration;
using FlaxMcp.Flax.Models;

namespace FlaxMcp.Flax;

/// <summary>
/// Reads the actor tree out of a .scene/.prefab JSON document. <c>Data[]</c> mixes two kinds of
/// entries linked via <c>ParentID</c>: actors (hierarchy edges) and scripts attached to an actor
/// (component edges). A script entry has no <c>Name</c> and its serialized fields live under a
/// top-level <c>"V"</c> property — but <c>"V"</c> alone is not a reliable script marker: some actors
/// (e.g. <c>FlaxEngine.UICanvas</c>) carry an empty <c>"V"</c> too. Verified against a real scene:
/// every one of its entries with a <c>Name</c> is an actor and every one without is a script, with no
/// exceptions, so entries are classified by <c>Name</c> presence, not <c>V</c> presence. Treating
/// every entry as a tree node (or using <c>V</c> as the marker) would show scripts, or actors that
/// happen to also carry <c>V</c>, as if they were nested child actors.
/// </summary>
public static class FlaxSceneReader
{
    private sealed record Entry(string Id, string TypeName, string? Name, string? ParentId, bool IsScript);

    private sealed class TreeBuildState
    {
        public int NodeCount;
        public bool Truncated;
    }

    public static FlaxSceneOutline ReadOutline(string filePath)
    {
        var (rootId, engineBuild, entries) = ReadEnvelope(filePath);

        // TryAdd rather than ToDictionary: a malformed document with a duplicate actor ID keeps the
        // first occurrence instead of throwing.
        var actorsById = new Dictionary<string, Entry>();
        foreach (var actor in entries.Where(e => !e.IsScript))
        {
            actorsById.TryAdd(actor.Id, actor);
        }

        var scriptsByParentId = entries
                                 .Where(e => e.IsScript && e.ParentId is not null)
                                 .GroupBy(e => e.ParentId!)
                                 .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(e => e.TypeName).ToArray());
        var childrenByParentId = actorsById.Values
                                            .Where(a => a.ParentId is not null && actorsById.ContainsKey(a.ParentId))
                                            .GroupBy(a => a.ParentId!)
                                            .ToDictionary(g => g.Key, g => g.Select(a => a.Id).ToArray());

        var roots = actorsById.Values.Where(a => a.ParentId is null || !actorsById.ContainsKey(a.ParentId));

        var state = new TreeBuildState();
        var rootNodes = new List<FlaxSceneActorNode>();
        foreach (var root in roots)
        {
            if (state.NodeCount >= ResponseLimits.DefaultMaxItems)
            {
                state.Truncated = true;
                break;
            }
            rootNodes.Add(BuildNode(root, actorsById, scriptsByParentId, childrenByParentId, depth: 0, state));
        }

        return new FlaxSceneOutline(rootId, engineBuild, rootNodes, state.Truncated);
    }

    public static IReadOnlyList<FlaxSceneActorInfo> FindActors(string filePath, string? name, string? typeName)
    {
        var (_, _, entries) = ReadEnvelope(filePath);

        return entries
               .Where(e => !e.IsScript)
               .Where(e => name is null || (e.Name?.Contains(name, StringComparison.OrdinalIgnoreCase) ?? false))
               .Where(e => typeName is null || string.Equals(e.TypeName, typeName, StringComparison.OrdinalIgnoreCase))
               .Select(e => new FlaxSceneActorInfo(e.Id, e.TypeName, e.Name, e.ParentId))
               .Take(ResponseLimits.DefaultListTop)
               .ToArray();
    }

    private static FlaxSceneActorNode BuildNode(
        Entry actor,
        IReadOnlyDictionary<string, Entry> actorsById,
        IReadOnlyDictionary<string, IReadOnlyList<string>> scriptsByParentId,
        IReadOnlyDictionary<string, string[]> childrenByParentId,
        int depth,
        TreeBuildState state)
    {
        state.NodeCount++;

        var scripts = scriptsByParentId.GetValueOrDefault(actor.Id, []);
        var children = new List<FlaxSceneActorNode>();

        if (childrenByParentId.TryGetValue(actor.Id, out var childIds))
        {
            if (depth + 1 > ResponseLimits.DefaultMaxDepth)
            {
                state.Truncated = true;
            }
            else
            {
                foreach (var childId in childIds)
                {
                    if (state.NodeCount >= ResponseLimits.DefaultMaxItems)
                    {
                        state.Truncated = true;
                        break;
                    }

                    children.Add(BuildNode(actorsById[childId], actorsById, scriptsByParentId, childrenByParentId, depth + 1, state));
                }
            }
        }

        return new FlaxSceneActorNode(actor.Id, actor.TypeName, actor.Name, scripts, children);
    }

    private static (string RootId, int? EngineBuild, List<Entry> Entries) ReadEnvelope(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new InvalidOperationException($"Scene file '{filePath}' does not exist.");
        }

        using var stream = File.OpenRead(filePath);
        JsonObject document;
        try
        {
            document = JsonNode.Parse(stream) as JsonObject ??
                throw new InvalidOperationException($"'{filePath}' is not a valid scene/prefab JSON document.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"'{filePath}' is not a valid scene/prefab JSON document.", ex);
        }

        var rootId = TryGetString(document, "ID") ?? string.Empty;
        var engineBuild = TryGetInt(document, "EngineBuild");

        var entries = new List<Entry>();
        if (document["Data"] is JsonArray dataArray)
        {
            foreach (var node in dataArray)
            {
                if (TryParseEntry(node, out var entry))
                {
                    entries.Add(entry);
                }
            }
        }

        return (rootId, engineBuild, entries);
    }

    private static bool TryParseEntry(JsonNode? node, out Entry entry)
    {
        entry = null!;
        if (node is not JsonObject entryObject)
        {
            return false;
        }

        var id = TryGetString(entryObject, "ID");
        if (id is null)
        {
            return false;
        }

        var name = TryGetString(entryObject, "Name");
        entry = new Entry(
            id,
            TryGetString(entryObject, "TypeName") ?? string.Empty,
            name,
            TryGetString(entryObject, "ParentID"),
            IsScript: name is null
        );
        return true;
    }

    private static string? TryGetString(JsonObject document, string property)
    {
        try
        {
            return document[property]?.GetValue<string>();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static int? TryGetInt(JsonObject document, string property)
    {
        try
        {
            return document[property]?.GetValue<int>();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
