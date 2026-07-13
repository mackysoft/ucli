namespace MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;

/// <summary> Represents normalized payload values for one daemon launch-attempt projection. </summary>
internal sealed record DaemonLaunchAttemptOutput (
    string LaunchAttemptId,
    DaemonStartupStatus StartupStatus,
    DaemonStartupBlockingReason? StartupBlockingReason,
    DaemonStartupRetryDisposition RetryDisposition,
    DaemonStartupProcessAction ProcessAction,
    string ArtifactPath,
    string? UnityLogPath,
    DateTimeOffset UpdatedAtUtc,
    int? ProcessId,
    DateTimeOffset? ProcessStartedAtUtc,
    DaemonDiagnosisOutput Diagnosis);
