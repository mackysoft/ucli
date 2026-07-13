using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Shared.CommandContracts;

namespace MackySoft.Ucli.Application.Features.Play.Common.Contracts;

/// <summary> Represents normalized lifecycle snapshot values emitted by Play Mode command payloads. </summary>
internal sealed record PlayLifecycleSnapshotOutput (
    string? ServerVersion,
    string? EditorMode,
    string? UnityVersion,
    ProjectFingerprint? ProjectFingerprint,
    string? LifecycleState,
    string? BlockingReason,
    string? CompileState,
    string? CompileGeneration,
    string? DomainReloadGeneration,
    bool CanAcceptExecutionRequests,
    DateTimeOffset? ObservedAtUtc,
    string? ActionRequired,
    DaemonPrimaryDiagnosticOutput? PrimaryDiagnostic,
    PlayModeSnapshotOutput PlayMode);
