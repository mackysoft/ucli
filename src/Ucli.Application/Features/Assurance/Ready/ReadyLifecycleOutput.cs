using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Represents normalized Unity lifecycle evidence emitted by ready. </summary>
internal sealed record ReadyLifecycleOutput (
    string? ServerVersion,
    string? UnityVersion,
    DaemonEditorMode? EditorMode,
    IpcEditorLifecycleState? LifecycleState,
    IpcEditorBlockingReason? BlockingReason,
    IpcCompileState? CompileState,
    IpcUnityGenerationSnapshot? Generations,
    bool CanAcceptExecutionRequests,
    DateTimeOffset? ObservedAtUtc,
    DaemonDiagnosisActionRequired? ActionRequired,
    ReadyPrimaryDiagnosticOutput? PrimaryDiagnostic,
    IpcPlayModeSnapshot? PlayMode);
