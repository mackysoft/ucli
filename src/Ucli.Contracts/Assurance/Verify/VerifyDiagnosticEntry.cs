namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Represents the <c>verify.diagnostic</c> stream payload. </summary>
public readonly record struct VerifyDiagnosticEntry (
    string Code,
    string Message,
    string Severity,
    string? StepKind);
