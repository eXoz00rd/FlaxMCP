using System.Text.RegularExpressions;
using FlaxMcp.Flax.Models;

namespace FlaxMcp.Flax;

public static partial class FlaxBuildTargetReader
{
    public static FlaxBuildTargetInfo Read(string buildCsFilePath)
    {
        if (!File.Exists(buildCsFilePath))
        {
            throw new InvalidOperationException($"Target file '{buildCsFilePath}' does not exist.");
        }

        var text = File.ReadAllText(buildCsFilePath);

        var classMatch = ClassDeclarationPattern().Match(text);
        if (!classMatch.Success)
        {
            throw new InvalidOperationException($"'{buildCsFilePath}' does not contain a recognizable target class declaration.");
        }

        var modules = ModuleAddPattern()
            .Matches(text)
            .Select(match => match.Groups[1].Value)
            .ToArray();

        return new FlaxBuildTargetInfo(
            Name: classMatch.Groups[1].Value,
            BaseClass: classMatch.Groups[2].Value,
            Modules: modules
        );
    }

    [GeneratedRegex(@"class\s+(\w+)\s*:\s*(\w+)")]
    private static partial Regex ClassDeclarationPattern();

    [GeneratedRegex("""Modules\.Add\(\s*"([^"]+)"\s*\)""")]
    private static partial Regex ModuleAddPattern();
}
