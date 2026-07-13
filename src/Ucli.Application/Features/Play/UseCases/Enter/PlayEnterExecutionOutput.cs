using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Play.UseCases.Enter;

/// <summary> Represents normalized output payload values for one Play Mode enter command execution. </summary>
internal sealed record PlayEnterExecutionOutput (
    ProjectIdentityInfo Project,
    DaemonStatusKind DaemonStatus,
    string? ServerVersion,
    DaemonEditorMode EditorMode,
    IpcEditorLifecycleState? LifecycleState,
    IpcEditorBlockingReason? BlockingReason,
    IpcCompileState? CompileState,
    IpcUnityGenerationSnapshot? Generations,
    bool CanAcceptExecutionRequests,
    DateTimeOffset? ObservedAtUtc,
    string? ActionRequired,
    DaemonPrimaryDiagnosticOutput? PrimaryDiagnostic,
    IpcPlayModeSnapshot PlayMode,
    PlayEnterTransitionOutput Transition,
    int TimeoutMilliseconds);
