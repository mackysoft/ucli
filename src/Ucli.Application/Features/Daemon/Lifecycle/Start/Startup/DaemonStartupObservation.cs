namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Startup;

/// <summary> Represents endpoint-registration startup observation data for one daemon start failure. </summary>
internal sealed record DaemonStartupObservation (
    string StartupStatus,
    string? StartupBlockingReason,
    string? LaunchAttemptId,
    string ProcessAction,
    string RetryDisposition,
    string? EditorMode = null,
    string? OwnerKind = null,
    bool? CanShutdownProcess = null,
    int? ProcessId = null,
    DateTimeOffset? StartedAtUtc = null,
    int? ElapsedMilliseconds = null,
    string? ArtifactPath = null);
