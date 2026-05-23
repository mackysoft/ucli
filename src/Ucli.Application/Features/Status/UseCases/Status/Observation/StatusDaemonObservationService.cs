using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Status.UseCases.Status.Projection;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Status.UseCases.Status.Observation;

/// <summary> Resolves daemon status and optional ping diagnostics for status command payload projection. </summary>
internal sealed class StatusDaemonObservationService : IStatusDaemonObservationService
{
    private readonly IDaemonStatusOperation daemonStatusOperation;

    private readonly IDaemonPingInfoClient daemonPingInfoClient;

    private readonly IDaemonReachabilityClassifier reachabilityClassifier;

    private readonly IDaemonLifecycleStore daemonLifecycleStore;

    private readonly IDaemonProcessIdentityAssessor processIdentityAssessor;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="StatusDaemonObservationService" /> class. </summary>
    /// <param name="daemonStatusOperation"> The daemon status-operation dependency. </param>
    /// <param name="daemonPingInfoClient"> The daemon ping-info client dependency. </param>
    /// <param name="reachabilityClassifier"> The daemon reachability classifier dependency. </param>
    /// <param name="daemonLifecycleStore"> The daemon lifecycle observation store dependency. </param>
    /// <param name="processIdentityAssessor"> The daemon process identity assessor dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public StatusDaemonObservationService (
        IDaemonStatusOperation daemonStatusOperation,
        IDaemonPingInfoClient daemonPingInfoClient,
        IDaemonReachabilityClassifier reachabilityClassifier,
        IDaemonLifecycleStore daemonLifecycleStore,
        IDaemonProcessIdentityAssessor processIdentityAssessor,
        TimeProvider? timeProvider = null)
    {
        this.daemonStatusOperation = daemonStatusOperation ?? throw new ArgumentNullException(nameof(daemonStatusOperation));
        this.daemonPingInfoClient = daemonPingInfoClient ?? throw new ArgumentNullException(nameof(daemonPingInfoClient));
        this.reachabilityClassifier = reachabilityClassifier ?? throw new ArgumentNullException(nameof(reachabilityClassifier));
        this.daemonLifecycleStore = daemonLifecycleStore ?? throw new ArgumentNullException(nameof(daemonLifecycleStore));
        this.processIdentityAssessor = processIdentityAssessor ?? throw new ArgumentNullException(nameof(processIdentityAssessor));
        this.timeProvider = timeProvider ?? TimeProvider.System;
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

        if (daemonStatusResult.Status != DaemonStatusKind.Running)
        {
            if (daemonStatusResult.Status == DaemonStatusKind.Stale && daemonStatusResult.Session is not null)
            {
                return await CreateUnreachableObservationAsync(
                        context.UnityProject,
                        daemonStatusResult.Session,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

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
            var pingResponse = await daemonPingInfoClient.PingAndReadAsync(
                    context.UnityProject,
                    timeout,
                    daemonStatusResult.Session.SessionToken,
                    cancellationToken: cancellationToken)
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
        catch (TimeoutException)
        {
            return await CreateUnreachableObservationAsync(
                    context.UnityProject,
                    daemonStatusResult.Session,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (reachabilityClassifier.IsNotRunning(exception))
        {
            return await CreateUnreachableObservationAsync(
                    context.UnityProject,
                    daemonStatusResult.Session,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            return StatusDaemonObservationResult.Failure(ExecutionError.InternalError(
                $"Failed to read daemon ping information. {exception.Message}"));
        }
    }

    private async ValueTask<StatusDaemonObservationResult> CreateUnreachableObservationAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        CancellationToken cancellationToken)
    {
        var lifecycleReadResult = await daemonLifecycleStore.ReadAsync(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        var observation = lifecycleReadResult.Observation;
        if (lifecycleReadResult.IsSuccess
            && lifecycleReadResult.Exists
            && observation is not null
            && DaemonLifecycleObservationAvailability.IsUsableForSession(
                observation,
                session,
                processIdentityAssessor,
                timeProvider))
        {
            return StatusDaemonObservationResult.Success(
                StatusDaemonObservationCodec.CreateFromLifecycleObservation(
                    DaemonStatusKind.Running,
                    observation));
        }

        return StatusDaemonObservationResult.Success(
            StatusDaemonObservationCodec.CreateUnavailable(DaemonStatusKind.Stale));
    }
}
