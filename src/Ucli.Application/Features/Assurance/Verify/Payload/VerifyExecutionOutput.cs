namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Payload;

/// <summary> Represents the verify assurance payload emitted by the <c>verify</c> command. </summary>
internal sealed record VerifyExecutionOutput (
    string Verdict,
    ProjectIdentityInfo Project,
    IReadOnlyList<VerifyVerifierOutput> Verifiers,
    IReadOnlyList<VerifyClaimOutput> Claims,
    IReadOnlyDictionary<string, VerifyReportOutput> Reports,
    IReadOnlyList<VerifyResidualRiskOutput> ResidualRisks,
    VerifyProfileOutput Profile,
    int TimeoutMilliseconds);
