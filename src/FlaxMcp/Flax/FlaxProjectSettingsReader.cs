using System.Text.Json;
using System.Text.Json.Nodes;
using FlaxMcp.Flax.Models;

namespace FlaxMcp.Flax;

public static class FlaxProjectSettingsReader
{
    public static IReadOnlyList<FlaxSettingsFile> ReadAll(string projectDirectory)
    {
        var contentDirectory = Path.Combine(projectDirectory, "Content");
        var files = new List<FlaxSettingsFile>();

        CollectFrom(contentDirectory, files);
        CollectFrom(Path.Combine(contentDirectory, "Settings"), files);

        return files;
    }

    private static void CollectFrom(string directory, List<FlaxSettingsFile> files)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var filePath in Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
        {
            if (TryRead(filePath, out var settingsFile))
            {
                files.Add(settingsFile);
            }
        }
    }

    private static bool TryRead(string filePath, out FlaxSettingsFile settingsFile)
    {
        settingsFile = null!;

        JsonObject? document;
        try
        {
            using var stream = File.OpenRead(filePath);
            document = JsonNode.Parse(stream) as JsonObject;
        }
        catch (JsonException)
        {
            return false;
        }

        if (document?["Data"] is null)
        {
            return false;
        }

        settingsFile = new FlaxSettingsFile(
            Name: Path.GetFileNameWithoutExtension(filePath),
            TypeName: document["TypeName"]?.GetValue<string>() ?? string.Empty,
            Data: document["Data"]
        );
        return true;
    }
}
