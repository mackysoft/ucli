using MackySoft.Ucli.Application.Shared.Context;

namespace MackySoft.Ucli.Application.Features.Status.UseCases.Status.Observation;

/// <summary> Observes daemon status and runtime diagnostics for the status workflow. </summary>
internal interface IStatusDaemonObservationService
{
    /// <summary> Resolves daemon status and optional ping diagnostics for one status execution. </summary>
    /// <param name="context"> The resolved shared project context. </param>
    /// <param name="timeout"> The effective timeout used for daemon probing. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the daemon observation result. </returns>
    ValueTask<StatusDaemonObservationResult> ObserveAsync (
        ProjectContext context,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
