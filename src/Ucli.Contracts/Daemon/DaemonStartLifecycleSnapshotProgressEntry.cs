namespace MackySoft.Ucli.Contracts.Daemon;

/// <summary> Represents one <c>daemon.start</c> endpoint-registered lifecycle progress payload. </summary>
public sealed record DaemonStartLifecycleSnapshotProgressEntry (
    string PayloadKind,
    string ProjectFingerprint,
    int TimeoutMilliseconds,
    string? EditorMode,
    string OnStartupBlocked,
    string LifecycleState,
    string? BlockingReason,
    bool CanAcceptExecutionRequests);
