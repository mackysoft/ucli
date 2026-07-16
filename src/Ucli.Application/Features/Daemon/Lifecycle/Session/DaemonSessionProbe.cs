using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

/// <summary> Pings exact session snapshots and returns only project-validated observations while following safe generation recovery. </summary>
internal sealed class DaemonSessionProbe
{
    private readonly DaemonSessionAcquisitionCoordinator sessionAcquisitionCoordinator;

    private readonly IDaemonPingInfoClient daemonPingInfoClient;

    private readonly IDaemonReachabilityClassifier reachabilityClassifier;

    /// <summary> Initializes a new instance of the <see cref="DaemonSessionProbe" /> class. </summary>
    /// <param name="sessionAcquisitionCoordinator"> The coordinator that creates one acquisition scope per logical request. </param>
    /// <param name="daemonPingInfoClient"> The exact-session ping client dependency. </param>
    /// <param name="reachabilityClassifier"> The reachability classifier used to identify token rotation. </param>
    public DaemonSessionProbe (
        DaemonSessionAcquisitionCoordinator sessionAcquisitionCoordinator,
        IDaemonPingInfoClient daemonPingInfoClient,
        IDaemonReachabilityClassifier reachabilityClassifier)
    {
        this.sessionAcquisitionCoordinator = sessionAcquisitionCoordinator ?? throw new ArgumentNullException(nameof(sessionAcquisitionCoordinator));
        this.daemonPingInfoClient = daemonPingInfoClient ?? throw new ArgumentNullException(nameof(daemonPingInfoClient));
        this.reachabilityClassifier = reachabilityClassifier ?? throw new ArgumentNullException(nameof(reachabilityClassifier));
    }

    /// <summary> Pings the observed session and follows safe generation changes within the shared deadline. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="observedSession"> The exact session generation observed by the caller. </param>
    /// <param name="deadline"> The single deadline shared with the caller workflow. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> The responding session and project-validated ping, or the refreshed-session read failure. </returns>
    public async ValueTask<DaemonSessionProbeResult> ProbeAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession observedSession,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(observedSession);
        ArgumentNullException.ThrowIfNull(deadline);
        var requestId = Guid.NewGuid();

        var acquisitionScope = sessionAcquisitionCoordinator.CreateScope(deadline);
        var currentSession = observedSession;
        DaemonSessionAcquisitionResult? sessionAcquisition = null;
        Exception? tokenRejection = null;
        Exception? latestPreWriteFailure = null;
        while (true)
        {
            if (sessionAcquisition is not null)
            {
                switch (sessionAcquisition.Kind)
                {
                    case DaemonSessionAcquisitionKind.Success:
                        currentSession = sessionAcquisition.Session!;
                        sessionAcquisition = null;
                        break;
                    case DaemonSessionAcquisitionKind.RequestDeadlineExpired:
                        return DaemonSessionProbeResult.ProbeFailed(
                            currentSession,
                            new TimeoutException("Timed out while acquiring a daemon session for reachability probe."));
                    case DaemonSessionAcquisitionKind.PublicationWindowExpired:
                        return DaemonSessionProbeResult.ProbeFailed(currentSession, tokenRejection!);
                    case DaemonSessionAcquisitionKind.EndpointAvailabilityWindowExpired:
                        return DaemonSessionProbeResult.ProbeFailed(currentSession, latestPreWriteFailure!);
                    case DaemonSessionAcquisitionKind.SessionNotAvailable:
                        return DaemonSessionProbeResult.ProbeFailed(
                            currentSession,
                            new InvalidOperationException(
                                DaemonSessionAcquisitionResult.SessionNotAvailableMessage));
                    case DaemonSessionAcquisitionKind.SessionReadFailure:
                        return DaemonSessionProbeResult.SessionReadFailed(
                            currentSession,
                            sessionAcquisition.ReadFailure!);
                    default:
                        throw new InvalidOperationException(
                            $"Unsupported daemon session acquisition outcome: {sessionAcquisition.Kind}.");
                }
            }

            try
            {
                var pingResponse = await PingWithinDeadlineAsync(
                        unityProject,
                        currentSession,
                        requestId,
                        deadline,
                        cancellationToken)
                    .ConfigureAwait(false);
                return DaemonSessionProbeResult.Success(currentSession, pingResponse);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (reachabilityClassifier.IsSessionTokenInvalid(exception))
            {
                tokenRejection = exception;
                latestPreWriteFailure = null;
                sessionAcquisition = await acquisitionScope.ResolveReplacementAsync(
                        unityProject,
                        currentSession,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (
                reachabilityClassifier.IsRetryableBeforeRequestWrite(exception))
            {
                latestPreWriteFailure = exception;
                sessionAcquisition = await acquisitionScope.ResolveAfterPreWriteFailureAsync(
                        unityProject,
                        currentSession,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (
                reachabilityClassifier.IsRecoverableResponseInterruption(exception)
                && !deadline.IsExpired)
            {
                latestPreWriteFailure = exception;
                sessionAcquisition = await acquisitionScope.ResolveAfterStatelessResponseInterruptionAsync(
                        unityProject,
                        currentSession,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                return DaemonSessionProbeResult.ProbeFailed(currentSession, exception);
            }
        }
    }

    private async ValueTask<IpcUnityEditorObservation> PingWithinDeadlineAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        Guid requestId,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        if (!deadline.TryGetRemainingTimeout(out _))
        {
            throw new TimeoutException("Timed out before daemon session probe could begin.");
        }

        var pingOperation = await ExecutionDeadlineOperation.ExecuteAsync(
                deadline,
                cancellationToken,
                "Timed out before daemon session probe could begin.",
                "Timed out while probing daemon session.",
                token => daemonPingInfoClient.PingSessionAndReadAsync(
                    unityProject,
                    session,
                    requestId,
                    deadline,
                    validateProjectFingerprint: true,
                    cancellationToken: token))
            .ConfigureAwait(false);
        if (!pingOperation.IsSuccess)
        {
            throw new TimeoutException(pingOperation.Error!.Message);
        }

        return pingOperation.Value!;
    }
}
