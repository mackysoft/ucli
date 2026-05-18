namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Represents the ready assurance payload emitted by the <c>ready</c> command. </summary>
internal sealed record ReadyExecutionOutput (
    string Verdict,
    ProjectIdentityInfo Project,
    IReadOnlyList<ReadyVerifierOutput> Verifiers,
    IReadOnlyList<ReadyClaimOutput> Claims,
    IReadOnlyDictionary<string, ReadyReportOutput> Reports,
    IReadOnlyList<ReadyResidualRiskOutput> ResidualRisks,
    string Target,
    string RequestedMode,
    string ResolvedMode,
    string SessionKind,
    int TimeoutMilliseconds,
    ReadyLifecycleOutput? Lifecycle = null,
    ReadyReadIndexOutput? ReadIndex = null);
