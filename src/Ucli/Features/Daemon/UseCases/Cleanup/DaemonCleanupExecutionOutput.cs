namespace MackySoft.Ucli.Features.Daemon.UseCases.Cleanup;

/// <summary> Represents normalized payload values for one daemon-cleanup command execution. </summary>
/// <param name="CleanupStatus"> The cleanup-status value. </param>
/// <param name="SkipReason"> The cleanup skip-reason value when cleanup was skipped; otherwise <see langword="null" />. </param>
/// <param name="TimeoutMilliseconds"> The effective timeout in milliseconds used for daemon cleanup workflow. </param>
internal sealed record DaemonCleanupExecutionOutput (
    string CleanupStatus,
    string? SkipReason,
    int TimeoutMilliseconds);