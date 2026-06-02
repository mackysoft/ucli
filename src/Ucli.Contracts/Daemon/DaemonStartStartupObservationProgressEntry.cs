namespace MackySoft.Ucli.Contracts.Daemon;

/// <summary> Represents one <c>daemon.start</c> startup-observation progress payload. </summary>
public sealed record DaemonStartStartupObservationProgressEntry (
    string PayloadKind,
    string ProjectFingerprint,
    int TimeoutMilliseconds,
    string? EditorMode,
    string OnStartupBlocked,
    string? LaunchAttemptId,
    string? OwnerKind,
    bool? CanShutdownProcess,
    int? ProcessId,
    DateTimeOffset? ProcessStartedAtUtc,
    string? StartupStatus,
    string? StartupBlockingReason,
    string? StartupPhase,
    string? RetryDisposition,
    string? Message,
    string? ErrorCode);
