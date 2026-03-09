using MackySoft.Ucli.Context;
using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Status;

/// <summary> Resolves daemon status and optional ping diagnostics for status command payload projection. </summary>
internal sealed class StatusDaemonObservationService : IStatusDaemonObservationService
{
    private readonly IDaemonStatusOperation daemonStatusOperation;

    private readonly IDaemonPingInfoClient daemonPingInfoClient;

    private readonly IDaemonReachabilityClassifier reachabilityClassifier;

    /// <summary> Initializes a new instance of the <see cref="StatusDaemonObservationService" /> class. </summary>
    /// <param name="daemonStatusOperation"> The daemon status-operation dependency. </param>
    /// <param name="daemonPingInfoClient"> The daemon ping-info client dependency. </param>
    /// <param name="reachabilityClassifier"> The daemon reachability classifier dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public StatusDaemonObservationService (
        IDaemonStatusOperation daemonStatusOperation,
        IDaemonPingInfoClient daemonPingInfoClient,
        IDaemonReachabilityClassifier reachabilityClassifier)
    {
        this.daemonStatusOperation = daemonStatusOperation ?? throw new ArgumentNullException(nameof(daemonStatusOperation));
        this.daemonPingInfoClient = daemonPingInfoClient ?? throw new ArgumentNullException(nameof(daemonPingInfoClient));
        this.reachabilityClassifier = reachabilityClassifier ?? throw new ArgumentNullException(nameof(reachabilityClassifier));
    }

    /// <summary> Resolves daemon status and optional ping diagnostics for one status execution. </summary>
    /// <param name="context"> The resolved shared project context. </param>
    /// <param name="timeout"> The effective timeout used for daemon probing. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the daemon observation result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="context" /> is <see langword="null" />. </exception>
    public async ValueTask<StatusDaemonObservationResult> Observe (
        ProjectContext context,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var daemonStatusResult = await daemonStatusOperation.GetStatus(context.UnityProject, timeout, cancellationToken).ConfigureAwait(false);
        if (!daemonStatusResult.IsSuccess)
        {
            return StatusDaemonObservationResult.Failure(daemonStatusResult.Error!);
        }

        if (daemonStatusResult.Status != DaemonStatusKind.Running)
        {
            return StatusDaemonObservationResult.Success(
                StatusDaemonObservationCodec.CreateWithoutPing(daemonStatusResult.Status));
        }

        if (daemonStatusResult.Session is null)
        {
            return StatusDaemonObservationResult.Failure(ExecutionError.InternalError(
                "Daemon status is running but daemon session is missing."));
        }

        try
        {
            var pingResponse = await daemonPingInfoClient.PingAndRead(
                    context.UnityProject,
                    timeout,
                    daemonStatusResult.Session.SessionToken,
                    cancellationToken)
                .ConfigureAwait(false);

            return StatusDaemonObservationResult.Success(
                StatusDaemonObservationCodec.CreateFromPing(
                    daemonStatusResult.Status,
                    pingResponse));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException exception)
        {
            return StatusDaemonObservationResult.Failure(ExecutionError.Timeout(
                $"Timed out while reading daemon ping information. {exception.Message}"));
        }
        catch (Exception exception) when (reachabilityClassifier.IsNotRunning(exception))
        {
            return StatusDaemonObservationResult.Success(
                StatusDaemonObservationCodec.CreateWithoutPing(DaemonStatusKind.Stale));
        }
        catch (Exception exception)
        {
            return StatusDaemonObservationResult.Failure(ExecutionError.InternalError(
                $"Failed to read daemon ping information. {exception.Message}"));
        }
    }
}