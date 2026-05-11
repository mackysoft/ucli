namespace MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;

/// <summary> Represents normalized payload values for one daemon launch-attempt projection. </summary>
internal sealed record DaemonLaunchAttemptOutput (
    string LaunchAttemptId,
    string StartupStatus,
    string? StartupBlockingReason,
    string RetryDisposition,
    string ProcessAction,
    string ArtifactPath,
    string? UnityLogPath,
    DateTimeOffset UpdatedAtUtc,
    int? ProcessId,
    DateTimeOffset? ProcessStartedAtUtc,
    DaemonDiagnosisOutput Diagnosis);
