using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Daemon;

/// <summary> Represents one <c>daemon.start</c> endpoint-registered lifecycle progress payload. </summary>
public sealed record DaemonStartLifecycleSnapshotProgressEntry
{
    /// <summary> Initializes one validated endpoint-registered lifecycle progress payload. </summary>
    [JsonConstructor]
    public DaemonStartLifecycleSnapshotProgressEntry (
        string PayloadKind,
        ProjectFingerprint ProjectFingerprint,
        int TimeoutMilliseconds,
        string? EditorMode,
        string OnStartupBlocked,
        string LifecycleState,
        string? BlockingReason,
        bool CanAcceptExecutionRequests)
    {
        this.PayloadKind = ContractArgumentGuard.RequireValue(PayloadKind, nameof(PayloadKind));
        this.ProjectFingerprint = ContractArgumentGuard.RequireNotNull(ProjectFingerprint, nameof(ProjectFingerprint));
        this.TimeoutMilliseconds = ContractArgumentGuard.RequireNonNegative(TimeoutMilliseconds, nameof(TimeoutMilliseconds));
        this.EditorMode = EditorMode;
        this.OnStartupBlocked = ContractArgumentGuard.RequireValue(OnStartupBlocked, nameof(OnStartupBlocked));
        this.LifecycleState = ContractArgumentGuard.RequireValue(LifecycleState, nameof(LifecycleState));
        this.BlockingReason = BlockingReason;
        this.CanAcceptExecutionRequests = CanAcceptExecutionRequests;
    }

    public string PayloadKind { get; }

    public ProjectFingerprint ProjectFingerprint { get; }

    public int TimeoutMilliseconds { get; }

    public string? EditorMode { get; }

    public string OnStartupBlocked { get; }

    public string LifecycleState { get; }

    public string? BlockingReason { get; }

    public bool CanAcceptExecutionRequests { get; }
}
