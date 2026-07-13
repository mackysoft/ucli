namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Startup;

/// <summary> Represents endpoint-registration startup observation data for one daemon start failure. </summary>
internal sealed record DaemonStartupObservation (
    DaemonStartupStatus StartupStatus,
    DaemonStartupBlockingReason? StartupBlockingReason,
    string? LaunchAttemptId,
    DaemonStartupProcessAction ProcessAction,
    DaemonStartupRetryDisposition RetryDisposition,
    DaemonEditorMode? EditorMode = null,
    DaemonSessionOwnerKind? OwnerKind = null,
    bool? CanShutdownProcess = null,
    int? ProcessId = null,
    DateTimeOffset? StartedAtUtc = null,
    int? ElapsedMilliseconds = null,
    string? ArtifactPath = null);
