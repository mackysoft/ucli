using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;

/// <summary> Represents normalized payload values for one daemon-status command execution. </summary>
/// <param name="DaemonStatus"> The daemon-status value. </param>
/// <param name="ServerVersion"> The daemon server version when available; otherwise <see langword="null" />. </param>
/// <param name="EditorMode"> The daemon Editor mode when available; otherwise <see langword="null" />. </param>
/// <param name="LifecycleState"> The daemon lifecycle-state value when available; otherwise <see langword="null" />. </param>
/// <param name="BlockingReason"> The daemon blocking-reason value when available; otherwise <see langword="null" />. </param>
/// <param name="CompileState"> The daemon compile-state value when available; otherwise <see langword="null" />. </param>
/// <param name="Generations"> The Unity lifecycle generation snapshot when available; otherwise <see langword="null" />. </param>
/// <param name="CanAcceptExecutionRequests"> Whether execution requests can currently be accepted. </param>
/// <param name="ObservedAtUtc"> The daemon lifecycle observation timestamp when available. </param>
/// <param name="ActionRequired"> The normalized user action required by the lifecycle blocker when available. </param>
/// <param name="PrimaryDiagnostic"> The primary lifecycle diagnostic when available. </param>
/// <param name="TimeoutMilliseconds"> The effective timeout in milliseconds used for daemon status workflow. </param>
/// <param name="Session"> The daemon session values when available; otherwise <see langword="null" />. </param>
/// <param name="Diagnosis"> The daemon diagnosis values when available; otherwise <see langword="null" />. </param>
/// <param name="LastLaunchAttempt"> The last session-less launch-attempt failure when available; otherwise <see langword="null" />. </param>
/// <param name="PlayMode"> The Play Mode snapshot when daemon ping details are available. </param>
internal sealed record DaemonStatusExecutionOutput (
    DaemonStatusKind DaemonStatus,
    string? ServerVersion,
    DaemonEditorMode? EditorMode,
    IpcEditorLifecycleState? LifecycleState,
    IpcEditorBlockingReason? BlockingReason,
    IpcCompileState? CompileState,
    IpcUnityGenerationSnapshot? Generations,
    bool CanAcceptExecutionRequests,
    int TimeoutMilliseconds,
    DaemonSessionOutput? Session,
    DaemonDiagnosisOutput? Diagnosis,
    DaemonLaunchAttemptOutput? LastLaunchAttempt,
    DateTimeOffset? ObservedAtUtc,
    DaemonDiagnosisActionRequired? ActionRequired,
    DaemonPrimaryDiagnosticOutput? PrimaryDiagnostic,
    IpcPlayModeSnapshot? PlayMode);
