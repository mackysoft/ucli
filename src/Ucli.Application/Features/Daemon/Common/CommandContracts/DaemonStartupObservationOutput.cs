namespace MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;

/// <summary> Represents normalized payload values for one daemon startup observation. </summary>
internal sealed record DaemonStartupObservationOutput (
    string StartupStatus,
    string? StartupBlockingReason,
    string? LaunchAttemptId,
    string? EditorMode,
    string? OwnerKind,
    bool? CanShutdownProcess,
    int? ProcessId,
    DateTimeOffset? StartedAtUtc,
    int? ElapsedMilliseconds,
    string ProcessAction,
    object? ProcessTermination,
    string? ArtifactPath,
    string RetryDisposition);
