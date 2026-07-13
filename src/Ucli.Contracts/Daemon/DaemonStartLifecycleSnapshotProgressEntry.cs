using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Daemon;

/// <summary> Represents one <c>daemon.start</c> endpoint-registered lifecycle progress payload. </summary>
public sealed record DaemonStartLifecycleSnapshotProgressEntry (
    DaemonStartProgressPayloadKind PayloadKind,
    string ProjectFingerprint,
    int TimeoutMilliseconds,
    DaemonEditorMode? EditorMode,
    DaemonStartupBlockedProcessPolicy OnStartupBlocked,
    IpcEditorLifecycleState LifecycleState,
    IpcEditorBlockingReason? BlockingReason,
    IpcUnityGenerationSnapshot Generations,
    bool CanAcceptExecutionRequests);
