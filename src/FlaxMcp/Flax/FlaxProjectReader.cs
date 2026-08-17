using System.Text.Json.Nodes;
using FlaxMcp.Flax.Models;

namespace FlaxMcp.Flax;

public static class FlaxProjectReader
{
    public static FlaxProjectInfo Read(string projectFilePath)
    {
        using var stream = File.OpenRead(projectFilePath);
        var document = JsonNode.Parse(stream) as JsonObject ??
            throw new InvalidOperationException($"'{projectFilePath}' is not a valid .flaxproj JSON document.");

        var references = document["References"] as JsonArray;
        var referenceNames = references?
            .Select(reference => reference?["Name"]?.GetValue<string>())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .ToArray() ?? [];

        return new FlaxProjectInfo(
            Name: document["Name"]?.GetValue<string>() ?? Path.GetFileNameWithoutExtension(projectFilePath),
            Version: document["Version"]?.GetValue<string>() ?? "0.0",
            GameTarget: document["GameTarget"]?.GetValue<string>() ?? string.Empty,
            EditorTarget: document["EditorTarget"]?.GetValue<string>() ?? string.Empty,
            References: referenceNames,
            DefaultScene: document["DefaultScene"]?.GetValue<string>(),
            MinEngineVersion: document["MinEngineVersion"]?.GetValue<string>()
        );
    }
}
