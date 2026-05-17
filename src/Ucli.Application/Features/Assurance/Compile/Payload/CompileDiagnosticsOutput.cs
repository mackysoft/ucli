namespace MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;

/// <summary> Represents compiler diagnostic counts grouped under <c>payload.compile.scriptCompilation.diagnostics</c>. </summary>
internal sealed record CompileDiagnosticsOutput (
    int ErrorCount,
    int WarningCount,
    CompilePrimaryDiagnosticOutput? PrimaryDiagnostic);
