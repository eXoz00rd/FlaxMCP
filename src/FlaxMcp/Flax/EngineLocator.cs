using FlaxMcp.Configuration;

namespace FlaxMcp.Flax;

/// <summary>
/// Resolves the Flax Engine installation directory: an explicit override, or auto-detection via
/// the Flax Launcher's <c>Versions.txt</c> (<c>%APPDATA%\Flax\Versions.txt</c>).
/// </summary>
public static class EngineLocator
{
    public static string Resolve(string? explicitEnginePath)
    {
        if (!string.IsNullOrWhiteSpace(explicitEnginePath))
        {
            if (!IsValidEngineRoot(explicitEnginePath))
            {
                throw new InvalidOperationException(
                    $"{FlaxMcpOptions.EnginePathVariable} points to '{explicitEnginePath}', which does not contain Flax.flaxproj."
                );
            }
            return explicitEnginePath;
        }

        var versionsFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Flax",
            "Versions.txt"
        );
        if (!File.Exists(versionsFile))
        {
            throw new InvalidOperationException(
                $"Could not auto-detect the Flax Engine install (no '{versionsFile}'). Set {FlaxMcpOptions.EnginePathVariable} explicitly."
            );
        }

        var candidates = ParseVersionsFile(File.ReadAllLines(versionsFile)).Where(IsValidEngineRoot).Distinct().ToList();
        return candidates.Count switch
        {
            0 => throw new InvalidOperationException(
                $"No usable Flax Engine install found via '{versionsFile}'. Set {FlaxMcpOptions.EnginePathVariable} explicitly."
            ),
            1 => candidates[0],
            _ => throw new InvalidOperationException(
                $"Multiple Flax Engine installs found ({string.Join(", ", candidates)}). Set {FlaxMcpOptions.EnginePathVariable} to pick one."
            ),
        };
    }

    /// <summary>
    /// Parses the Flax Launcher's Versions.txt: alternating lines of a version id and its install path.
    /// </summary>
    internal static IEnumerable<string> ParseVersionsFile(IReadOnlyList<string> lines)
    {
        for (var i = 1; i < lines.Count; i += 2)
        {
            var path = lines[i].Trim();
            if (path.Length > 0)
            {
                yield return path;
            }
        }
    }

    /// <summary>
    /// Resolves the path to <c>FlaxEditor.exe</c> for the given engine install and editor build config.
    /// </summary>
    public static string ResolveEditorExecutable(string enginePath, string editorConfig)
    {
        return Path.Combine(enginePath, "Binaries", "Editor", "Win64", editorConfig, "FlaxEditor.exe");
    }

    private static bool IsValidEngineRoot(string path)
    {
        return File.Exists(Path.Combine(path, "Flax.flaxproj"));
    }
}
