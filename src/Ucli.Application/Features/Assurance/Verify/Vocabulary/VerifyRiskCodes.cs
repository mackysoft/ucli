namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Vocabulary;

/// <summary> Defines residual risk codes emitted by the verify command. </summary>
internal static class VerifyRiskCodes
{
    /// <summary> Gets the risk emitted when diagnostics cannot be bound to a generated post-read claim. </summary>
    public static readonly UcliCode FromDiagnosticCoverageUnbound = new("VERIFY_FROM_DIAGNOSTIC_COVERAGE_UNBOUND");

    /// <summary> Gets all verify-owned residual risk codes. </summary>
    public static IReadOnlyList<UcliCode> All { get; } =
    [
        FromDiagnosticCoverageUnbound,
    ];
}
