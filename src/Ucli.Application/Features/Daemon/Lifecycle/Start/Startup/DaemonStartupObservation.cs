
namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Startup;

/// <summary> Represents endpoint-registration startup observation data for one daemon start failure. </summary>
internal sealed record DaemonStartupObservation
{
    /// <summary> Initializes one daemon startup observation. </summary>
    public DaemonStartupObservation (
        DaemonStartupStatus StartupStatus,
        DaemonStartupBlockingReason StartupBlockingReason,
        Guid? LaunchAttemptId,
        DaemonStartupProcessAction ProcessAction,
        DaemonStartupRetryDisposition RetryDisposition,
        DaemonEditorMode? EditorMode,
        DaemonSessionOwnerKind? OwnerKind,
        bool? CanShutdownProcess,
        int? ProcessId,
        DateTimeOffset? StartedAtUtc,
        int? ElapsedMilliseconds,
        string? ArtifactPath)
    {
        if (StartupStatus is not (DaemonStartupStatus.Blocked
            or DaemonStartupStatus.Timeout
            or DaemonStartupStatus.Failed))
        {
            throw new ArgumentOutOfRangeException(
                nameof(StartupStatus),
                StartupStatus,
                "Unsupported daemon startup failure status.");
        }

        if (!TextVocabulary.IsDefined(StartupBlockingReason))
        {
            throw new ArgumentOutOfRangeException(
                nameof(StartupBlockingReason),
                StartupBlockingReason,
                "Unsupported daemon startup blocking reason.");
        }

        if (LaunchAttemptId == Guid.Empty)
        {
            throw new ArgumentException("Launch attempt identifier must not be empty.", nameof(LaunchAttemptId));
        }

        if (!TextVocabulary.IsDefined(ProcessAction))
        {
            throw new ArgumentOutOfRangeException(
                nameof(ProcessAction),
                ProcessAction,
                "Unsupported daemon startup process action.");
        }

        if (!TextVocabulary.IsDefined(RetryDisposition))
        {
            throw new ArgumentOutOfRangeException(
                nameof(RetryDisposition),
                RetryDisposition,
                "Unsupported daemon startup retry disposition.");
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

        if (StartedAtUtc.HasValue
            && (StartedAtUtc.Value == default || StartedAtUtc.Value.Offset != TimeSpan.Zero))
        {
            throw new ArgumentException(
                "Process start time must be a non-default UTC value.",
                nameof(StartedAtUtc));
        }

        if (ElapsedMilliseconds is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ElapsedMilliseconds),
                ElapsedMilliseconds,
                "Elapsed milliseconds must not be negative.");
        }

        this.StartupStatus = StartupStatus;
        this.StartupBlockingReason = StartupBlockingReason;
        this.LaunchAttemptId = LaunchAttemptId;
        this.ProcessAction = ProcessAction;
        this.RetryDisposition = RetryDisposition;
        this.EditorMode = EditorMode;
        this.OwnerKind = OwnerKind;
        this.CanShutdownProcess = CanShutdownProcess;
        this.ProcessId = ProcessId;
        this.StartedAtUtc = StartedAtUtc;
        this.ElapsedMilliseconds = ElapsedMilliseconds;
        this.ArtifactPath = ArtifactPath;
    }

    public DaemonStartupStatus StartupStatus { get; }

    public DaemonStartupBlockingReason StartupBlockingReason { get; }

    public Guid? LaunchAttemptId { get; }

    public DaemonStartupProcessAction ProcessAction { get; }

    public DaemonStartupRetryDisposition RetryDisposition { get; }

    public DaemonEditorMode? EditorMode { get; }

    public DaemonSessionOwnerKind? OwnerKind { get; }

    public bool? CanShutdownProcess { get; }

    public int? ProcessId { get; }

    public DateTimeOffset? StartedAtUtc { get; }

    public int? ElapsedMilliseconds { get; }

    public string? ArtifactPath { get; }
}
