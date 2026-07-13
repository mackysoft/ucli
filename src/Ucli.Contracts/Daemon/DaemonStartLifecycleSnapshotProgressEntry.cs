using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Daemon;

/// <summary> Represents one <c>daemon.start</c> endpoint-registered lifecycle progress payload. </summary>
public sealed record DaemonStartLifecycleSnapshotProgressEntry
{
    /// <summary> Initializes one validated endpoint-registered lifecycle progress payload. </summary>
    [JsonConstructor]
    public DaemonStartLifecycleSnapshotProgressEntry (
        DaemonStartProgressPayloadKind PayloadKind,
        ProjectFingerprint ProjectFingerprint,
        int TimeoutMilliseconds,
        DaemonEditorMode? EditorMode,
        DaemonStartupBlockedProcessPolicy OnStartupBlocked,
        IpcEditorLifecycleState LifecycleState,
        IpcEditorBlockingReason? BlockingReason,
        IpcUnityGenerationSnapshot Generations,
        bool CanAcceptExecutionRequests)
    {
        this.PayloadKind = PayloadKind;
        this.ProjectFingerprint = ContractArgumentGuard.RequireNotNull(ProjectFingerprint, nameof(ProjectFingerprint));
        this.TimeoutMilliseconds = ContractArgumentGuard.RequireNonNegative(TimeoutMilliseconds, nameof(TimeoutMilliseconds));
        this.EditorMode = EditorMode;
        this.OnStartupBlocked = OnStartupBlocked;
        this.LifecycleState = LifecycleState;
        this.BlockingReason = BlockingReason;
        this.Generations = ContractArgumentGuard.RequireNotNull(Generations, nameof(Generations));
        this.CanAcceptExecutionRequests = CanAcceptExecutionRequests;
    }

    public DaemonStartProgressPayloadKind PayloadKind { get; }

    public ProjectFingerprint ProjectFingerprint { get; }

    public int TimeoutMilliseconds { get; }

    public DaemonEditorMode? EditorMode { get; }

    public DaemonStartupBlockedProcessPolicy OnStartupBlocked { get; }

    public IpcEditorLifecycleState LifecycleState { get; }

    public IpcEditorBlockingReason? BlockingReason { get; }

    public IpcUnityGenerationSnapshot Generations { get; }

    public bool CanAcceptExecutionRequests { get; }
}
