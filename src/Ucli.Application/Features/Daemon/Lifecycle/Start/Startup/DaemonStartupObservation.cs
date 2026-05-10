namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Startup;

/// <summary> Represents endpoint-registration startup observation data for one daemon start failure. </summary>
internal sealed record DaemonStartupObservation (
    string StartupStatus,
    string? StartupBlockingReason,
    string? LaunchAttemptId,
    string? EditorMode,
    string? OwnerKind,
    bool? CanShutdownProcess,
    int? ProcessId,
    DateTimeOffset? StartedAtUtc,
    long? ElapsedMilliseconds,
    string ProcessAction,
    DaemonStartupProcessTermination? ProcessTermination,
    string? ArtifactPath,
    string RetryDisposition,
    bool SafeToRetryImmediately);
