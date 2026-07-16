namespace MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;

/// <summary> Represents normalized payload values for one daemon startup observation. </summary>
internal sealed record DaemonStartupObservationOutput
{
    /// <summary> Initializes one daemon startup observation projection. </summary>
    public DaemonStartupObservationOutput (
        DaemonStartupStatus StartupStatus,
        DaemonStartupBlockingReason? StartupBlockingReason,
        Guid? LaunchAttemptId,
        DaemonEditorMode? EditorMode,
        DaemonSessionOwnerKind? OwnerKind,
        bool? CanShutdownProcess,
        int? ProcessId,
        DateTimeOffset? StartedAtUtc,
        int? ElapsedMilliseconds,
        DaemonStartupProcessAction ProcessAction,
        object? ProcessTermination,
        string? ArtifactPath,
        DaemonStartupRetryDisposition RetryDisposition)
    {
        if (LaunchAttemptId == Guid.Empty)
        {
            throw new ArgumentException("Launch attempt identifier must not be empty.", nameof(LaunchAttemptId));
        }

        this.StartupStatus = StartupStatus;
        this.StartupBlockingReason = StartupBlockingReason;
        this.LaunchAttemptId = LaunchAttemptId;
        this.EditorMode = EditorMode;
        this.OwnerKind = OwnerKind;
        this.CanShutdownProcess = CanShutdownProcess;
        this.ProcessId = ProcessId;
        this.StartedAtUtc = StartedAtUtc;
        this.ElapsedMilliseconds = ElapsedMilliseconds;
        this.ProcessAction = ProcessAction;
        this.ProcessTermination = ProcessTermination;
        this.ArtifactPath = ArtifactPath;
        this.RetryDisposition = RetryDisposition;
    }

    public DaemonStartupStatus StartupStatus { get; }

    public DaemonStartupBlockingReason? StartupBlockingReason { get; }

    public Guid? LaunchAttemptId { get; }

    public DaemonEditorMode? EditorMode { get; }

    public DaemonSessionOwnerKind? OwnerKind { get; }

    public bool? CanShutdownProcess { get; }

    public int? ProcessId { get; }

    public DateTimeOffset? StartedAtUtc { get; }

    public int? ElapsedMilliseconds { get; }

    public DaemonStartupProcessAction ProcessAction { get; }

    public object? ProcessTermination { get; }

    public string? ArtifactPath { get; }

    public DaemonStartupRetryDisposition RetryDisposition { get; }
}
