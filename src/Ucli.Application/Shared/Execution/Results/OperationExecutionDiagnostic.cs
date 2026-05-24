namespace MackySoft.Ucli.Application.Shared.Execution.Results;

/// <summary> Represents one non-fatal diagnostic attached to a public operation result. </summary>
/// <param name="Code"> The stable diagnostic code. </param>
/// <param name="Severity"> The diagnostic severity literal. </param>
/// <param name="CoverageImpact"> The diagnostic coverage-impact literal. </param>
/// <param name="Message"> The human-readable diagnostic message. </param>
internal sealed record OperationExecutionDiagnostic (
    UcliCode Code,
    string Severity,
    string CoverageImpact,
    string Message);
