using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.LaunchAttempts;

/// <summary> Represents one persisted daemon launch attempt that ended before session registration completed. </summary>
internal sealed record DaemonLaunchAttempt (
    string LaunchAttemptId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string StartupStatus,
    string? StartupBlockingReason,
    string RetryDisposition,
    string ProcessAction,
    string? EditorMode,
    int? ProcessId,
    DateTimeOffset? ProcessStartedAtUtc,
    string? UnityLogPath,
    string ArtifactPath,
    DaemonDiagnosis Diagnosis);
