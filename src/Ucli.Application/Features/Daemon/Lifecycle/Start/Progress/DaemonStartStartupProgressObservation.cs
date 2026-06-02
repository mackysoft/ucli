namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Progress;

/// <summary> Represents one daemon-start startup observation before public progress payload projection. </summary>
internal sealed record DaemonStartStartupProgressObservation (
    string? LaunchAttemptId,
    string? EditorMode,
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
