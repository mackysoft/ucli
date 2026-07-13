using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Daemon;

/// <summary> Represents one <c>daemon.start</c> startup-observation progress payload. </summary>
public sealed record DaemonStartStartupObservationProgressEntry
{
    /// <summary> Initializes one validated startup-observation progress payload. </summary>
    [JsonConstructor]
    public DaemonStartStartupObservationProgressEntry (
        string PayloadKind,
        ProjectFingerprint ProjectFingerprint,
        int TimeoutMilliseconds,
        string? EditorMode,
        string OnStartupBlocked,
        string? LaunchAttemptId,
        string? OwnerKind,
        bool? CanShutdownProcess,
        int? ProcessId,
        DateTimeOffset? ProcessStartedAtUtc,
        string? StartupStatus,
        string? StartupBlockingReason,
        string? StartupPhase,
        string? RetryDisposition,
        string? Message,
        string? ErrorCode)
    {
        this.PayloadKind = ContractArgumentGuard.RequireValue(PayloadKind, nameof(PayloadKind));
        this.ProjectFingerprint = ContractArgumentGuard.RequireNotNull(ProjectFingerprint, nameof(ProjectFingerprint));
        this.TimeoutMilliseconds = ContractArgumentGuard.RequireNonNegative(TimeoutMilliseconds, nameof(TimeoutMilliseconds));
        this.EditorMode = EditorMode;
        this.OnStartupBlocked = ContractArgumentGuard.RequireValue(OnStartupBlocked, nameof(OnStartupBlocked));
        this.LaunchAttemptId = LaunchAttemptId;
        this.OwnerKind = OwnerKind;
        this.CanShutdownProcess = CanShutdownProcess;
        this.ProcessId = ProcessId;
        this.ProcessStartedAtUtc = ProcessStartedAtUtc;
        this.StartupStatus = StartupStatus;
        this.StartupBlockingReason = StartupBlockingReason;
        this.StartupPhase = StartupPhase;
        this.RetryDisposition = RetryDisposition;
        this.Message = Message;
        this.ErrorCode = ErrorCode;
    }

    public string PayloadKind { get; }

    public ProjectFingerprint ProjectFingerprint { get; }

    public int TimeoutMilliseconds { get; }

    public string? EditorMode { get; }

    public string OnStartupBlocked { get; }

    public string? LaunchAttemptId { get; }

    public string? OwnerKind { get; }

    public bool? CanShutdownProcess { get; }

    public int? ProcessId { get; }

    public DateTimeOffset? ProcessStartedAtUtc { get; }

    public string? StartupStatus { get; }

    public string? StartupBlockingReason { get; }

    public string? StartupPhase { get; }

    public string? RetryDisposition { get; }

    public string? Message { get; }

    public string? ErrorCode { get; }
}
