using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Contracts.Daemon;

/// <summary> Represents one <c>daemon.start</c> startup-observation progress payload. </summary>
public sealed record DaemonStartStartupObservationProgressEntry
{
    /// <summary> Initializes one validated startup-observation progress payload. </summary>
    [JsonConstructor]
    public DaemonStartStartupObservationProgressEntry (
        DaemonStartProgressPayloadKind PayloadKind,
        ProjectFingerprint ProjectFingerprint,
        int TimeoutMilliseconds,
        DaemonEditorMode? EditorMode,
        DaemonStartupBlockedProcessPolicy OnStartupBlocked,
        string? LaunchAttemptId,
        DaemonSessionOwnerKind? OwnerKind,
        bool? CanShutdownProcess,
        int? ProcessId,
        DateTimeOffset? ProcessStartedAtUtc,
        DaemonStartupStatus? StartupStatus,
        DaemonStartupBlockingReason? StartupBlockingReason,
        DaemonDiagnosisStartupPhase? StartupPhase,
        DaemonStartupRetryDisposition? RetryDisposition,
        string? Message,
        string? ErrorCode)
    {
        this.PayloadKind = PayloadKind;
        this.ProjectFingerprint = ContractArgumentGuard.RequireNotNull(ProjectFingerprint, nameof(ProjectFingerprint));
        this.TimeoutMilliseconds = ContractArgumentGuard.RequireNonNegative(TimeoutMilliseconds, nameof(TimeoutMilliseconds));
        this.EditorMode = EditorMode;
        this.OnStartupBlocked = OnStartupBlocked;
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

    public DaemonStartProgressPayloadKind PayloadKind { get; }

    public ProjectFingerprint ProjectFingerprint { get; }

    public int TimeoutMilliseconds { get; }

    public DaemonEditorMode? EditorMode { get; }

    public DaemonStartupBlockedProcessPolicy OnStartupBlocked { get; }

    public string? LaunchAttemptId { get; }

    public DaemonSessionOwnerKind? OwnerKind { get; }

    public bool? CanShutdownProcess { get; }

    public int? ProcessId { get; }

    public DateTimeOffset? ProcessStartedAtUtc { get; }

    public DaemonStartupStatus? StartupStatus { get; }

    public DaemonStartupBlockingReason? StartupBlockingReason { get; }

    public DaemonDiagnosisStartupPhase? StartupPhase { get; }

    public DaemonStartupRetryDisposition? RetryDisposition { get; }

    public string? Message { get; }

    public string? ErrorCode { get; }
}
