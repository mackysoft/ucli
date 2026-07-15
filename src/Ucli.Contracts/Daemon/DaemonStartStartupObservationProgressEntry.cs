using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Daemon;

/// <summary> Represents one <c>daemon.start</c> startup-observation progress payload. </summary>
public sealed record DaemonStartStartupObservationProgressEntry
{
    /// <summary> Initializes one validated startup-observation progress payload. </summary>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="LaunchAttemptId" /> is empty or an optional text value is blank. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="ProjectFingerprint" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="TimeoutMilliseconds" /> is negative, a finite contract value is undefined, or <paramref name="PayloadKind" /> does not identify a startup observation. </exception>
    [JsonConstructor]
    public DaemonStartStartupObservationProgressEntry (
        DaemonStartProgressPayloadKind PayloadKind,
        ProjectFingerprint ProjectFingerprint,
        int TimeoutMilliseconds,
        DaemonEditorMode? EditorMode,
        DaemonStartupBlockedProcessPolicy OnStartupBlocked,
        Guid? LaunchAttemptId,
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
        if (PayloadKind != DaemonStartProgressPayloadKind.StartupObservation)
        {
            throw new ArgumentOutOfRangeException(nameof(PayloadKind), PayloadKind, "Payload kind must identify a startup observation.");
        }

        if (EditorMode.HasValue && !ContractLiteralCodec.IsDefined(EditorMode.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(EditorMode), EditorMode, "Editor mode must be defined when specified.");
        }

        if (!ContractLiteralCodec.IsDefined(OnStartupBlocked))
        {
            throw new ArgumentOutOfRangeException(nameof(OnStartupBlocked), OnStartupBlocked, "Startup-blocked process policy must be defined.");
        }

        if (LaunchAttemptId == Guid.Empty)
        {
            throw new ArgumentException("Launch attempt identifier must not be empty.", nameof(LaunchAttemptId));
        }

        if (OwnerKind.HasValue && !ContractLiteralCodec.IsDefined(OwnerKind.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(OwnerKind), OwnerKind, "Session owner kind must be defined when specified.");
        }

        if (StartupStatus.HasValue && !ContractLiteralCodec.IsDefined(StartupStatus.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(StartupStatus), StartupStatus, "Startup status must be defined when specified.");
        }

        if (StartupBlockingReason.HasValue && !ContractLiteralCodec.IsDefined(StartupBlockingReason.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(StartupBlockingReason), StartupBlockingReason, "Startup blocking reason must be defined when specified.");
        }

        if (StartupPhase.HasValue && !ContractLiteralCodec.IsDefined(StartupPhase.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(StartupPhase), StartupPhase, "Startup phase must be defined when specified.");
        }

        if (RetryDisposition.HasValue && !ContractLiteralCodec.IsDefined(RetryDisposition.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(RetryDisposition), RetryDisposition, "Retry disposition must be defined when specified.");
        }

        if (Message is not null && string.IsNullOrWhiteSpace(Message))
        {
            throw new ArgumentException("Message must not be blank when specified.", nameof(Message));
        }

        if (ErrorCode is not null && string.IsNullOrWhiteSpace(ErrorCode))
        {
            throw new ArgumentException("Error code must not be blank when specified.", nameof(ErrorCode));
        }

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

    public Guid? LaunchAttemptId { get; }

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
