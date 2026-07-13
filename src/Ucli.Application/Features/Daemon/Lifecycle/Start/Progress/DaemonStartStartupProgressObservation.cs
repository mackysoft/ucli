namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Progress;

/// <summary> Represents one daemon-start startup observation before public progress payload projection. </summary>
internal sealed record DaemonStartStartupProgressObservation (
    string? LaunchAttemptId,
    DaemonEditorMode? EditorMode,
    DaemonSessionOwnerKind? OwnerKind,
    bool? CanShutdownProcess,
    int? ProcessId,
    DateTimeOffset? ProcessStartedAtUtc,
    DaemonStartupStatus? StartupStatus,
    DaemonStartupBlockingReason? StartupBlockingReason,
    MackySoft.Ucli.Contracts.Storage.DaemonDiagnosisStartupPhase? StartupPhase,
    DaemonStartupRetryDisposition? RetryDisposition,
    string? Message,
    string? ErrorCode);
