namespace FlaxMcp.Flax.Models;

/// <summary>
/// A single compiler diagnostic (error or warning) parsed out of an engine log by
/// <see cref="FlaxMcp.Flax.FlaxCompilerDiagnosticParser"/>.
/// </summary>
public sealed record FlaxCompilerDiagnostic(string File, int Line, int Column, string Severity, string Code, string Message);
