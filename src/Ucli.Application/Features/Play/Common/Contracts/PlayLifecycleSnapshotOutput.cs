using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Play.Common.Contracts;

/// <summary> Represents normalized lifecycle snapshot values emitted by Play Mode command payloads. </summary>
internal sealed record PlayLifecycleSnapshotOutput (
    string? ServerVersion,
    DaemonEditorMode? EditorMode,
    string? UnityVersion,
    string? ProjectFingerprint,
    IpcEditorLifecycleState? LifecycleState,
    IpcEditorBlockingReason? BlockingReason,
    IpcCompileState? CompileState,
    IpcUnityGenerationSnapshot? Generations,
    bool CanAcceptExecutionRequests,
    DateTimeOffset? ObservedAtUtc,
    string? ActionRequired,
    DaemonPrimaryDiagnosticOutput? PrimaryDiagnostic,
    IpcPlayModeSnapshot PlayMode);
