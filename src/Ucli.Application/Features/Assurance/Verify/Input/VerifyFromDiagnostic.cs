namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Input;

/// <summary> Represents one operation diagnostic consumed by post-read verification. </summary>
internal sealed record VerifyFromDiagnostic (
    string Code,
    string Severity,
    string CoverageImpact,
    string Message);
