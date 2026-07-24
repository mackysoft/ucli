
namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Progress;

/// <summary> Represents one daemon-start startup observation before public progress payload projection. </summary>
internal sealed record DaemonStartStartupProgressObservation
{
    /// <summary> Initializes one daemon-start startup observation. </summary>
    public DaemonStartStartupProgressObservation (
        Guid? LaunchAttemptId,
        DaemonEditorMode? EditorMode,
        DaemonSessionOwnerKind? OwnerKind,
        bool? CanShutdownProcess,
        int? ProcessId,
        DateTimeOffset? ProcessStartedAtUtc,
        DaemonStartupStatus? StartupStatus,
        DaemonStartupBlockingReason? StartupBlockingReason,
        MackySoft.Ucli.Contracts.Storage.DaemonDiagnosisStartupPhase? StartupPhase,
        DaemonStartupRetryDisposition? RetryDisposition,
        string? Message,
        string? ErrorCode)
    {
        if (LaunchAttemptId == Guid.Empty)
        {
            throw new ArgumentException("Launch attempt identifier must not be empty.", nameof(LaunchAttemptId));
        }

        if (EditorMode.HasValue && !TextVocabulary.IsDefined(EditorMode.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(EditorMode), EditorMode, "Unsupported daemon Editor mode.");
        }

        if (OwnerKind.HasValue && !TextVocabulary.IsDefined(OwnerKind.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(OwnerKind), OwnerKind, "Unsupported daemon session owner kind.");
        }

        if (ProcessId is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ProcessId), ProcessId, "Process identifier must be positive.");
        }

        if (ProcessStartedAtUtc.HasValue
            && (ProcessStartedAtUtc.Value == default || ProcessStartedAtUtc.Value.Offset != TimeSpan.Zero))
        {
            throw new ArgumentException(
                "Process start time must be a non-default UTC value.",
                nameof(ProcessStartedAtUtc));
        }

        if (StartupStatus.HasValue && !TextVocabulary.IsDefined(StartupStatus.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(StartupStatus), StartupStatus, "Unsupported daemon startup status.");
        }

        if (StartupBlockingReason.HasValue && !TextVocabulary.IsDefined(StartupBlockingReason.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(StartupBlockingReason),
                StartupBlockingReason,
                "Unsupported daemon startup blocking reason.");
        }

        if (StartupPhase.HasValue && !TextVocabulary.IsDefined(StartupPhase.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(StartupPhase), StartupPhase, "Unsupported daemon startup phase.");
        }

        if (RetryDisposition.HasValue && !TextVocabulary.IsDefined(RetryDisposition.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(RetryDisposition),
                RetryDisposition,
                "Unsupported daemon startup retry disposition.");
        }

        this.LaunchAttemptId = LaunchAttemptId;
        this.EditorMode = EditorMode;
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

    public Guid? LaunchAttemptId { get; }

    public DaemonEditorMode? EditorMode { get; }

    public DaemonSessionOwnerKind? OwnerKind { get; }

    public bool? CanShutdownProcess { get; }

    public int? ProcessId { get; }

    public DateTimeOffset? ProcessStartedAtUtc { get; }

    public DaemonStartupStatus? StartupStatus { get; }

    public DaemonStartupBlockingReason? StartupBlockingReason { get; }

    public MackySoft.Ucli.Contracts.Storage.DaemonDiagnosisStartupPhase? StartupPhase { get; }

    public DaemonStartupRetryDisposition? RetryDisposition { get; }

    public string? Message { get; }

    public string? ErrorCode { get; }
}
