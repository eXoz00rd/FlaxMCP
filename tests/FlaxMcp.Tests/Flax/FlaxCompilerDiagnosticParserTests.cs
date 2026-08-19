using FlaxMcp.Flax;
using Xunit;

namespace FlaxMcp.Tests.Flax;

public sealed class FlaxCompilerDiagnosticParserTests
{
    // Captured from a real headless FlaxEditor.exe run against a deliberately broken script. The
    // engine emits a four-number (startLine,startColumn,endLine,endColumn) span, and only the first
    // line of a batch carries the "[ 00:00:02.655 ]: [Info] " timestamp prefix.
    private const string RealBrokenScriptLog =
        "[ 00:00:02.539 ]: [Info] Compiling D:\\Projects\\Mournfall\\Binaries\\GameEditorTarget\\Windows\\x64\\Development\\Game.CSharp.dll\n" +
        "[ 00:00:02.655 ]: [Info] D:\\Projects\\Mournfall\\Source\\Game\\BrokenTestScript.cs(1,33,1,37): error CS1519: Invalid token 'this' in a member declaration\n" +
        "D:\\Projects\\Mournfall\\Source\\Game\\BrokenTestScript.cs(1,51,1,52): error CS1002: ; expected\n" +
        "D:\\Projects\\Mournfall\\Source\\Game\\BrokenTestScript.cs(1,58,1,58): error CS1519: Invalid token '' in a member declaration\n" +
        "[ 00:00:02.661 ]: [Error] Task failed with exit code 1\n";

    [Fact]
    public void Parse_WithRealBrokenScriptLog_ExtractsAllDiagnosticsInOrder()
    {
        var diagnostics = FlaxCompilerDiagnosticParser.Parse(RealBrokenScriptLog);

        Assert.Equal(3, diagnostics.Count);
        Assert.All(diagnostics, diagnostic => Assert.Equal(@"D:\Projects\Mournfall\Source\Game\BrokenTestScript.cs", diagnostic.File));
        Assert.All(diagnostics, diagnostic => Assert.Equal("error", diagnostic.Severity));
    }

    [Fact]
    public void Parse_WithRealBrokenScriptLog_ParsesFirstDiagnosticFieldsFromThePrefixedLine()
    {
        var diagnostics = FlaxCompilerDiagnosticParser.Parse(RealBrokenScriptLog);

        var first = diagnostics[0];
        Assert.Equal(1, first.Line);
        Assert.Equal(33, first.Column);
        Assert.Equal("CS1519", first.Code);
        Assert.Equal("Invalid token 'this' in a member declaration", first.Message);
    }

    [Fact]
    public void Parse_WithTwoNumberPositionForm_StillParses()
    {
        var diagnostics = FlaxCompilerDiagnosticParser.Parse(@"C:\Game\Foo.cs(10,5): warning CS0168: variable declared but never used");

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(10, diagnostic.Line);
        Assert.Equal(5, diagnostic.Column);
        Assert.Equal("warning", diagnostic.Severity);
        Assert.Equal("CS0168", diagnostic.Code);
    }

    [Fact]
    public void Parse_WithNoDiagnostics_ReturnsEmpty()
    {
        var diagnostics = FlaxCompilerDiagnosticParser.Parse("[Info] Compiled with no errors\n Total errors: 0\n");

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Parse_WithAParenthesizedPathSegment_StillFindsTheRealPositionMarker()
    {
        // "Program Files (x86)" is a common real-world Windows path segment. A file-path capture that
        // naively excludes '(' and ')' stops at the wrong parenthesis and fails to parse the diagnostic.
        var diagnostics = FlaxCompilerDiagnosticParser.Parse(@"C:\Program Files (x86)\Game\Source\Foo.cs(3,7): error CS1002: ; expected");

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(@"C:\Program Files (x86)\Game\Source\Foo.cs", diagnostic.File);
        Assert.Equal(3, diagnostic.Line);
        Assert.Equal(7, diagnostic.Column);
    }

    [Fact]
    public void Parse_WithATrailingNulByteInsteadOfALineBreak_StripsItFromTheMessage()
    {
        // Observed against a real headless run: the engine occasionally terminates the log's last
        // diagnostic on a line with an embedded NUL byte instead of \r\n (interleaved subprocess
        // output), which would otherwise leak a raw \0 into the structured Message field.
        var diagnostics = FlaxCompilerDiagnosticParser.Parse(@"D:\Game\Foo.cs(1,58,1,58): error CS1002: ; expected" + "\0");

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("; expected", diagnostic.Message);
    }
}
