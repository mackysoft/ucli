using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.LaunchAttempts;

/// <summary> Represents one persisted daemon launch attempt that ended before session registration completed. </summary>
internal sealed record DaemonLaunchAttempt (
    string LaunchAttemptId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DaemonStartupStatus StartupStatus,
    DaemonStartupBlockingReason? StartupBlockingReason,
    DaemonStartupRetryDisposition RetryDisposition,
    DaemonStartupProcessAction ProcessAction,
    DaemonEditorMode? EditorMode,
    int? ProcessId,
    DateTimeOffset? ProcessStartedAtUtc,
    string? UnityLogPath,
    string ArtifactPath,
    DaemonDiagnosis Diagnosis);
