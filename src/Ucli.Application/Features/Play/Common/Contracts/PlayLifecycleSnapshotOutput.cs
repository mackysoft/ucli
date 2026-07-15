using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Application.Features.Play.Common.Contracts;

/// <summary> Represents normalized lifecycle snapshot values emitted by Play Mode command payloads. </summary>
internal sealed record PlayLifecycleSnapshotOutput (
    string? ServerVersion,
    DaemonEditorMode? EditorMode,
    string? UnityVersion,
    ProjectFingerprint? ProjectFingerprint,
    IpcEditorLifecycleState? LifecycleState,
    IpcEditorBlockingReason? BlockingReason,
    IpcCompileState? CompileState,
    IpcUnityGenerationSnapshot? Generations,
    bool CanAcceptExecutionRequests,
    DateTimeOffset? ObservedAtUtc,
    DaemonDiagnosisActionRequired? ActionRequired,
    DaemonPrimaryDiagnosticOutput? PrimaryDiagnostic,
    IpcPlayModeSnapshot PlayMode);
