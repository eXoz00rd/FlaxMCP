using System.Text.RegularExpressions;
using FlaxMcp.Flax.Models;

namespace FlaxMcp.Flax;

/// <summary>
/// Parses Roslyn/csc-style diagnostics out of engine log text, e.g.
/// <c>D:\Project\Source\Game\Foo.cs(1,33,1,37): error CS1519: Invalid token 'this' in a member declaration</c>.
/// Verified against a real headless run with a deliberately broken script: the engine emits the
/// four-number <c>(startLine,startColumn,endLine,endColumn)</c> span form, not the plain two-number
/// <c>(line,column)</c> form some Roslyn tooling uses, so both are accepted. Also verified against a
/// real run: a line's final diagnostic can be terminated by an embedded NUL byte instead of a normal
/// line break (interleaved subprocess output in the log), which would otherwise leak into <see cref="FlaxCompilerDiagnostic.Message"/>.
/// The file-path capture allows parentheses (e.g. <c>C:\Program Files (x86)\...</c>) rather than
/// excluding them -- the required <c>\d+,\d+</c> right after the position-opening <c>(</c> is enough
/// for backtracking to skip past a non-numeric parenthesized path segment and find the real one.
/// </summary>
public static partial class FlaxCompilerDiagnosticParser
{
    public static IReadOnlyList<FlaxCompilerDiagnostic> Parse(string log)
    {
        return [
            .. DiagnosticPattern()
               .Matches(log)
               .Select(match => new FlaxCompilerDiagnostic(
                       File: match.Groups["file"].Value,
                       Line: int.Parse(match.Groups["line"].Value),
                       Column: int.Parse(match.Groups["column"].Value),
                       Severity: match.Groups["severity"].Value,
                       Code: match.Groups["code"].Value,
                       Message: match.Groups["message"].Value.TrimEnd('\0')
                   )
               ),
        ];
    }

    [GeneratedRegex(@"(?<file>[A-Za-z]:[^\r\n]+?)\((?<line>\d+),(?<column>\d+)(?:,\d+,\d+)?\):\s*(?<severity>error|warning)\s+(?<code>[A-Z]+\d+):\s*(?<message>[^\r\n]+)")]
    private static partial Regex DiagnosticPattern();
}
