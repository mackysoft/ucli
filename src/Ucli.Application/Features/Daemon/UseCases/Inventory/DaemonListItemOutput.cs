using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
namespace MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;

/// <summary> Represents one daemon registration observation returned by <c>daemon list</c>. </summary>
/// <param name="WorktreePath"> The Git worktree root path. </param>
/// <param name="BranchRef"> The branch ref when attached; otherwise <see langword="null" />. </param>
/// <param name="Head"> The HEAD commit hash. </param>
/// <param name="ProjectPath"> The Unity project root path under the worktree. </param>
/// <param name="ProjectFingerprint"> The deterministic project fingerprint resolved for the worktree-local storage root. </param>
/// <param name="State"> The daemon observation state. </param>
/// <param name="Reason"> The optional failure reason for non-running states. </param>
/// <param name="IssuedAtUtc"> The daemon session issuance timestamp when valid session metadata is available; otherwise <see langword="null" />. </param>
/// <param name="ProcessId"> The daemon process identifier when valid session metadata is available; otherwise <see langword="null" />. </param>
/// <param name="ProcessStartedAtUtc"> The daemon process start timestamp when valid session metadata is available; otherwise <see langword="null" />. </param>
/// <param name="EditorMode"> The daemon Editor mode when valid session metadata is available; otherwise <see langword="null" />. </param>
/// <param name="OwnerKind"> The daemon owner kind when valid session metadata is available; otherwise <see langword="null" />. </param>
/// <param name="CanShutdownProcess"> Whether daemon process shutdown is allowed when valid session metadata is available; otherwise <see langword="null" />. </param>
/// <param name="EndpointTransportKind"> The daemon endpoint transport kind when valid session metadata is available; otherwise <see langword="null" />. </param>
/// <param name="EndpointAddress"> The daemon endpoint address when valid session metadata is available; otherwise <see langword="null" />. </param>
/// <param name="LifecycleState"> The daemon lifecycle-state value when observed; otherwise <see langword="null" />. </param>
/// <param name="BlockingReason"> The daemon blocking-reason value when observed; otherwise <see langword="null" />. </param>
/// <param name="CompileState"> The daemon compile-state value when observed; otherwise <see langword="null" />. </param>
/// <param name="CompileGeneration"> The daemon compile generation when observed; otherwise <see langword="null" />. </param>
/// <param name="DomainReloadGeneration"> The daemon domain-reload generation when observed; otherwise <see langword="null" />. </param>
/// <param name="CanAcceptExecutionRequests"> Whether execution requests can currently be accepted when observed; otherwise <see langword="null" />. </param>
/// <param name="ObservedAtUtc"> The daemon lifecycle observation timestamp when available. </param>
/// <param name="ActionRequired"> The normalized user action required by the lifecycle blocker when available. </param>
/// <param name="PrimaryDiagnostic"> The primary lifecycle diagnostic when available. </param>
/// <param name="Diagnosis"> The daemon diagnosis values when available; otherwise <see langword="null" />. </param>
internal sealed record DaemonListItemOutput (
    string WorktreePath,
    string? BranchRef,
    string Head,
    string ProjectPath,
    string ProjectFingerprint,
    DaemonListItemState State,
    DaemonListItemReason? Reason,
    DateTimeOffset? IssuedAtUtc,
    int? ProcessId,
    DateTimeOffset? ProcessStartedAtUtc,
    string? EditorMode,
    string? OwnerKind,
    bool? CanShutdownProcess,
    string? EndpointTransportKind,
    string? EndpointAddress,
    string? LifecycleState,
    string? BlockingReason,
    string? CompileState,
    string? CompileGeneration,
    string? DomainReloadGeneration,
    bool? CanAcceptExecutionRequests,
    DateTimeOffset? ObservedAtUtc,
    string? ActionRequired,
    DaemonPrimaryDiagnosticOutput? PrimaryDiagnostic,
    DaemonDiagnosisOutput? Diagnosis);
