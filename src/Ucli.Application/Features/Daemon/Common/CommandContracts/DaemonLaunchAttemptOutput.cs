namespace MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;

/// <summary> Represents normalized payload values for one daemon launch-attempt projection. </summary>
internal sealed record DaemonLaunchAttemptOutput
{
    /// <summary> Initializes one daemon launch-attempt projection. </summary>
    public DaemonLaunchAttemptOutput (
        Guid LaunchAttemptId,
        DaemonStartupStatus StartupStatus,
        DaemonStartupBlockingReason? StartupBlockingReason,
        DaemonStartupRetryDisposition RetryDisposition,
        DaemonStartupProcessAction ProcessAction,
        string ArtifactPath,
        string? UnityLogPath,
        DateTimeOffset UpdatedAtUtc,
        int? ProcessId,
        DateTimeOffset? ProcessStartedAtUtc,
        DaemonDiagnosisOutput Diagnosis)
    {
        if (LaunchAttemptId == Guid.Empty)
        {
            throw new ArgumentException("Launch attempt identifier must not be empty.", nameof(LaunchAttemptId));
        }

        this.LaunchAttemptId = LaunchAttemptId;
        this.StartupStatus = StartupStatus;
        this.StartupBlockingReason = StartupBlockingReason;
        this.RetryDisposition = RetryDisposition;
        this.ProcessAction = ProcessAction;
        this.ArtifactPath = ArtifactPath;
        this.UnityLogPath = UnityLogPath;
        this.UpdatedAtUtc = UpdatedAtUtc;
        this.ProcessId = ProcessId;
        this.ProcessStartedAtUtc = ProcessStartedAtUtc;
        this.Diagnosis = Diagnosis;
    }

    public Guid LaunchAttemptId { get; }

    public DaemonStartupStatus StartupStatus { get; }

    public DaemonStartupBlockingReason? StartupBlockingReason { get; }

    public DaemonStartupRetryDisposition RetryDisposition { get; }

    public DaemonStartupProcessAction ProcessAction { get; }

    public string ArtifactPath { get; }

    public string? UnityLogPath { get; }

    public DateTimeOffset UpdatedAtUtc { get; }

    public int? ProcessId { get; }

    public DateTimeOffset? ProcessStartedAtUtc { get; }

    public DaemonDiagnosisOutput Diagnosis { get; }
}
