using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Contracts.Daemon;

/// <summary> Represents one <c>daemon.start</c> startup-observation progress payload. </summary>
public sealed record DaemonStartStartupObservationProgressEntry (
    DaemonStartProgressPayloadKind PayloadKind,
    string ProjectFingerprint,
    int TimeoutMilliseconds,
    DaemonEditorMode? EditorMode,
    DaemonStartupBlockedProcessPolicy OnStartupBlocked,
    string? LaunchAttemptId,
    DaemonSessionOwnerKind? OwnerKind,
    bool? CanShutdownProcess,
    int? ProcessId,
    DateTimeOffset? ProcessStartedAtUtc,
    DaemonStartupStatus? StartupStatus,
    DaemonStartupBlockingReason? StartupBlockingReason,
    DaemonDiagnosisStartupPhase? StartupPhase,
    DaemonStartupRetryDisposition? RetryDisposition,
    string? Message,
    string? ErrorCode);
