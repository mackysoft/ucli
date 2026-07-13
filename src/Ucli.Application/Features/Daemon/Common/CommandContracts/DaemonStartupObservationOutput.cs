namespace MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;

/// <summary> Represents normalized payload values for one daemon startup observation. </summary>
internal sealed record DaemonStartupObservationOutput (
    DaemonStartupStatus StartupStatus,
    DaemonStartupBlockingReason? StartupBlockingReason,
    string? LaunchAttemptId,
    DaemonEditorMode? EditorMode,
    DaemonSessionOwnerKind? OwnerKind,
    bool? CanShutdownProcess,
    int? ProcessId,
    DateTimeOffset? StartedAtUtc,
    int? ElapsedMilliseconds,
    DaemonStartupProcessAction ProcessAction,
    object? ProcessTermination,
    string? ArtifactPath,
    DaemonStartupRetryDisposition RetryDisposition);
