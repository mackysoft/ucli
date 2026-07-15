using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Status.UseCases.Status.Projection;
using MackySoft.Ucli.Application.Shared.Context;

namespace MackySoft.Ucli.Application.Features.Status.UseCases.Status.Observation;

/// <summary> Projects daemon status and ping diagnostics for status command payloads. </summary>
internal sealed class StatusDaemonObservationService : IStatusDaemonObservationService
{
    private readonly IDaemonStatusOperation daemonStatusOperation;

    /// <summary> Initializes a new instance of the <see cref="StatusDaemonObservationService" /> class. </summary>
    /// <param name="daemonStatusOperation"> The daemon status-operation dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public StatusDaemonObservationService (IDaemonStatusOperation daemonStatusOperation)
    {
        this.daemonStatusOperation = daemonStatusOperation ?? throw new ArgumentNullException(nameof(daemonStatusOperation));
    }

    /// <summary> Resolves daemon status and optional ping diagnostics for one status execution. </summary>
    /// <param name="context"> The resolved shared project context. </param>
    /// <param name="timeout"> The effective timeout used for daemon probing. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the daemon observation result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="context" /> is <see langword="null" />. </exception>
    public async ValueTask<StatusDaemonObservationResult> ObserveAsync (
        ProjectContext context,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var daemonStatusResult = await daemonStatusOperation.GetStatusAsync(context.UnityProject, timeout, cancellationToken).ConfigureAwait(false);
        if (!daemonStatusResult.IsSuccess)
        {
            return StatusDaemonObservationResult.Failure(daemonStatusResult.Error!);
        }

        var status = daemonStatusResult.Status.Value;
        if (status != DaemonStatusKind.Running)
        {
            return StatusDaemonObservationResult.Success(
                StatusDaemonObservationCodec.CreateWithoutPing(status));
        }

        return StatusDaemonObservationResult.Success(
            StatusDaemonObservationCodec.CreateFromPing(
                status,
                daemonStatusResult.PingResponse!));
    }
}
