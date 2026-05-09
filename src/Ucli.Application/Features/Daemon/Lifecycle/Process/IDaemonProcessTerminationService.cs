using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;

/// <summary> Stops daemon process by process identifier, including force-kill fallback. </summary>
internal interface IDaemonProcessTerminationService
{
    /// <summary> Ensures daemon process is stopped before timeout expires. </summary>
    /// <param name="processId"> The daemon process identifier when available. </param>
    /// <param name="expectedIssuedAtUtc"> The expected daemon-session issuance timestamp used for process identity verification. </param>
    /// <param name="timeout"> The process termination timeout. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The process termination result. </returns>
    ValueTask<DaemonSessionStoreOperationResult> EnsureStoppedAsync (
        int? processId,
        DateTimeOffset? expectedIssuedAtUtc,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
