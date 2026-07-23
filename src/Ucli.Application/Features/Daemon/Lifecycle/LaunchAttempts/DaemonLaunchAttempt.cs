using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.LaunchAttempts;

/// <summary> Represents one persisted daemon launch attempt that ended before session registration completed. </summary>
internal sealed record DaemonLaunchAttempt
{
    /// <summary> Initializes one daemon launch attempt. </summary>
    public DaemonLaunchAttempt (
        Guid LaunchAttemptId,
        DateTimeOffset StartedAtUtc,
        DateTimeOffset UpdatedAtUtc,
        DaemonStartupStatus StartupStatus,
        DaemonStartupBlockingReason StartupBlockingReason,
        DaemonStartupRetryDisposition RetryDisposition,
        DaemonStartupProcessAction ProcessAction,
        DaemonEditorMode? EditorMode,
        int? ProcessId,
        DateTimeOffset? ProcessStartedAtUtc,
        AbsolutePath? UnityLogPath,
        AbsolutePath ArtifactPath,
        DaemonDiagnosis Diagnosis)
    {
        if (LaunchAttemptId == Guid.Empty)
        {
            throw new ArgumentException("Launch attempt identifier must not be empty.", nameof(LaunchAttemptId));
        }

        if (StartedAtUtc == default || StartedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Launch attempt start time must be a non-default UTC value.",
                nameof(StartedAtUtc));
        }

        if (UpdatedAtUtc == default || UpdatedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Launch attempt update time must be a non-default UTC value.",
                nameof(UpdatedAtUtc));
        }

        if (UpdatedAtUtc < StartedAtUtc)
        {
            throw new ArgumentException(
                "Launch attempt update time must not precede its start time.",
                nameof(UpdatedAtUtc));
        }

        if (StartupStatus is not (DaemonStartupStatus.Blocked
            or DaemonStartupStatus.Timeout
            or DaemonStartupStatus.Failed
            or DaemonStartupStatus.Completed))
        {
            throw new ArgumentOutOfRangeException(
                nameof(StartupStatus),
                StartupStatus,
                "Unsupported terminal daemon startup status.");
        }

        if (!ContractLiteralCodec.IsDefined(StartupBlockingReason))
        {
            throw new ArgumentOutOfRangeException(
                nameof(StartupBlockingReason),
                StartupBlockingReason,
                "Unsupported daemon startup blocking reason.");
        }

        if (!ContractLiteralCodec.IsDefined(RetryDisposition))
        {
            throw new ArgumentOutOfRangeException(
                nameof(RetryDisposition),
                RetryDisposition,
                "Unsupported daemon startup retry disposition.");
        }

        if (!ContractLiteralCodec.IsDefined(ProcessAction))
        {
            throw new ArgumentOutOfRangeException(
                nameof(ProcessAction),
                ProcessAction,
                "Unsupported daemon startup process action.");
        }

        if (EditorMode.HasValue && !ContractLiteralCodec.IsDefined(EditorMode.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(EditorMode), EditorMode, "Unsupported daemon Editor mode.");
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

        if (ProcessStartedAtUtc > UpdatedAtUtc)
        {
            throw new ArgumentException(
                "Process start time must not follow the launch attempt update time.",
                nameof(ProcessStartedAtUtc));
        }

        this.LaunchAttemptId = LaunchAttemptId;
        this.StartedAtUtc = StartedAtUtc;
        this.UpdatedAtUtc = UpdatedAtUtc;
        this.StartupStatus = StartupStatus;
        this.StartupBlockingReason = StartupBlockingReason;
        this.RetryDisposition = RetryDisposition;
        this.ProcessAction = ProcessAction;
        this.EditorMode = EditorMode;
        this.ProcessId = ProcessId;
        this.ProcessStartedAtUtc = ProcessStartedAtUtc;
        this.UnityLogPath = UnityLogPath;
        this.ArtifactPath = ArtifactPath ?? throw new ArgumentNullException(nameof(ArtifactPath));
        this.Diagnosis = Diagnosis ?? throw new ArgumentNullException(nameof(Diagnosis));
    }

    public Guid LaunchAttemptId { get; }

    public DateTimeOffset StartedAtUtc { get; }

    public DateTimeOffset UpdatedAtUtc { get; }

    public DaemonStartupStatus StartupStatus { get; }

    public DaemonStartupBlockingReason StartupBlockingReason { get; }

    public DaemonStartupRetryDisposition RetryDisposition { get; }

    public DaemonStartupProcessAction ProcessAction { get; }

    public DaemonEditorMode? EditorMode { get; }

    public int? ProcessId { get; }

    public DateTimeOffset? ProcessStartedAtUtc { get; }

    public AbsolutePath? UnityLogPath { get; }

    public AbsolutePath ArtifactPath { get; }

    public DaemonDiagnosis Diagnosis { get; }
}
