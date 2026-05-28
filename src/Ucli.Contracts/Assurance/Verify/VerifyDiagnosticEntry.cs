namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Represents the <c>verify.diagnostic</c> stream payload. </summary>
public sealed record VerifyDiagnosticEntry (
    string Code,
    string Message,
    string Severity,
    string? StepKind);
