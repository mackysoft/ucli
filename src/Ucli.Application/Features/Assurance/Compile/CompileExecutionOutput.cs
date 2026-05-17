namespace MackySoft.Ucli.Application.Features.Assurance.Compile;

/// <summary> Represents the compile assurance payload emitted by the <c>compile</c> command. </summary>
internal sealed record CompileExecutionOutput (
    string Verdict,
    ProjectIdentityInfo Project,
    IReadOnlyList<CompileVerifierOutput> Verifiers,
    IReadOnlyList<CompileClaimOutput> Claims,
    IReadOnlyDictionary<string, CompileReportOutput> Reports,
    IReadOnlyList<CompileResidualRiskOutput> ResidualRisks,
    string RequestedMode,
    string ResolvedMode,
    string SessionKind,
    int TimeoutMilliseconds,
    CompileOutput Compile);
