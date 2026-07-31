using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Application.Features.Status.Common.Contracts;

/// <summary> Represents normalized output payload values for one status command execution. </summary>
/// <param name="DaemonStatus"> The daemon status value. </param>
/// <param name="UnityVersion"> The Unity editor version resolved from <c>ProjectVersion.txt</c>. </param>
/// <param name="ServerVersion"> The daemon-side server version when daemon is reachable; otherwise <see langword="null" />. </param>
/// <param name="LifecycleState"> The daemon-side lifecycle-state when daemon is reachable; otherwise <see langword="null" />. </param>
/// <param name="BlockingReason"> The daemon-side blocking-reason when daemon is reachable; otherwise <see langword="null" />. </param>
/// <param name="CompileState"> The daemon compile-state value when daemon is reachable; otherwise <see langword="null" />. </param>
/// <param name="Generations"> The Unity lifecycle generation snapshot when daemon is reachable; otherwise <see langword="null" />. </param>
/// <param name="CanAcceptExecutionRequests"> Whether execution requests can currently be accepted. </param>
/// <param name="EditorMode"> The daemon Editor mode when daemon is reachable; otherwise <see langword="null" />. </param>
/// <param name="ObservedAtUtc"> The daemon lifecycle observation timestamp when available. </param>
/// <param name="ActionRequired"> The normalized user action required by the lifecycle blocker when available. </param>
/// <param name="PrimaryDiagnostic"> The primary lifecycle diagnostic when available. </param>
/// <param name="PlayMode"> The Play Mode snapshot when daemon ping details are available. </param>
/// <param name="TimeoutMilliseconds"> The effective timeout in milliseconds used for daemon status probing. </param>
internal sealed record StatusExecutionOutput (
    DaemonStatusKind DaemonStatus,
    string UnityVersion,
    string? ServerVersion,
    UnityEditorLifecycleState? LifecycleState,
    UnityEditorBlockingReason? BlockingReason,
    UnityEditorCompileState? CompileState,
    UnityEditorGenerationSnapshot? Generations,
    bool CanAcceptExecutionRequests,
    UnityEditorMode? EditorMode,
    DateTimeOffset? ObservedAtUtc,
    UnityEditorActionRequired? ActionRequired,
    DaemonPrimaryDiagnosticOutput? PrimaryDiagnostic,
    UnityEditorPlayModeSnapshot? PlayMode,
    int TimeoutMilliseconds);
