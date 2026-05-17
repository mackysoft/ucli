namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one non-fatal diagnostic attached to a public execute step result. </summary>
/// <param name="Code"> The stable diagnostic code. </param>
/// <param name="Severity"> The diagnostic severity literal. </param>
/// <param name="CoverageImpact"> The diagnostic coverage impact literal. </param>
/// <param name="Message"> The human-readable diagnostic message. </param>
public sealed record IpcExecuteDiagnostic (
    UcliCodeValue Code,
    string Severity,
    string CoverageImpact,
    string Message);
