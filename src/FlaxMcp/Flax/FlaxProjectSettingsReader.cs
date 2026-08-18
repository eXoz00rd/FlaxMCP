using System.Text.Json;
using System.Text.Json.Nodes;
using FlaxMcp.Flax.Models;

namespace FlaxMcp.Flax;

public static class FlaxProjectSettingsReader
{
    private const string SettingsTypeNamePrefix = "FlaxEditor.Content.Settings.";

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
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return false;
        }

        var typeName = document?["TypeName"]?.GetValue<string>();
        if (typeName is null || !typeName.StartsWith(SettingsTypeNamePrefix, StringComparison.Ordinal) || document?["Data"] is null)
        {
            return false;
        }

        settingsFile = new FlaxSettingsFile(
            Name: Path.GetFileNameWithoutExtension(filePath),
            TypeName: typeName,
            Data: document["Data"]
        );
        return true;
    }
}
