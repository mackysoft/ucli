using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;

/// <summary> Pings one exact session generation and follows at most one observed token rotation. </summary>
internal sealed class DaemonSessionProbe
{
    private readonly IDaemonSessionStore daemonSessionStore;

    private readonly IDaemonPingInfoClient daemonPingInfoClient;

    private readonly IDaemonReachabilityClassifier reachabilityClassifier;

    /// <summary> Initializes a new instance of the <see cref="DaemonSessionProbe" /> class. </summary>
    /// <param name="daemonSessionStore"> The daemon session-store dependency used to observe one replacement generation. </param>
    /// <param name="daemonPingInfoClient"> The exact-session ping client dependency. </param>
    /// <param name="reachabilityClassifier"> The reachability classifier used to identify token rotation. </param>
    public DaemonSessionProbe (
        IDaemonSessionStore daemonSessionStore,
        IDaemonPingInfoClient daemonPingInfoClient,
        IDaemonReachabilityClassifier reachabilityClassifier)
    {
        this.daemonSessionStore = daemonSessionStore ?? throw new ArgumentNullException(nameof(daemonSessionStore));
        this.daemonPingInfoClient = daemonPingInfoClient ?? throw new ArgumentNullException(nameof(daemonPingInfoClient));
        this.reachabilityClassifier = reachabilityClassifier ?? throw new ArgumentNullException(nameof(reachabilityClassifier));
    }

    /// <summary> Pings the observed session and follows one distinct replacement after token rejection. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="observedSession"> The exact session generation observed by the caller. </param>
    /// <param name="deadline"> The single deadline shared with the caller workflow. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> The responding session and ping, or the refreshed-session read failure. </returns>
    public async ValueTask<DaemonSessionProbeResult> ProbeAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession observedSession,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(observedSession);

        IpcPingResponse pingResponse;
        try
        {
            pingResponse = await PingWithinDeadlineAsync(
                    unityProject,
                    observedSession,
                    deadline,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            if (!reachabilityClassifier.IsSessionTokenInvalid(exception))
            {
                return DaemonSessionProbeResult.ProbeFailed(observedSession, exception);
            }

            DaemonSessionReadResult refreshedReadResult;
            try
            {
                refreshedReadResult = await ReadRefreshedSessionWithinDeadlineAsync(
                        unityProject,
                        deadline,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception refreshException)
            {
                return DaemonSessionProbeResult.ProbeFailed(observedSession, refreshException);
            }

            if (!refreshedReadResult.IsSuccess)
            {
                return DaemonSessionProbeResult.SessionReadFailed(observedSession, refreshedReadResult);
            }

            if (!refreshedReadResult.Exists
                || string.Equals(
                    refreshedReadResult.Session!.SessionToken,
                    observedSession.SessionToken,
                    StringComparison.Ordinal))
            {
                return DaemonSessionProbeResult.ProbeFailed(observedSession, exception);
            }

            var replacementSession = refreshedReadResult.Session!;
            try
            {
                pingResponse = await PingWithinDeadlineAsync(
                        unityProject,
                        replacementSession,
                        deadline,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception replacementException)
            {
                return DaemonSessionProbeResult.ProbeFailed(replacementSession, replacementException);
            }

            return DaemonSessionProbeResult.Success(replacementSession, pingResponse);
        }

        return DaemonSessionProbeResult.Success(observedSession, pingResponse);
    }

    private async ValueTask<DaemonSessionReadResult> ReadRefreshedSessionWithinDeadlineAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        var refreshedSessionRead = await ExecutionDeadlineOperation.ExecuteAsync(
                deadline,
                cancellationToken,
                "Timed out before daemon session refresh could begin.",
                "Timed out while refreshing daemon session after token rejection.",
                token => daemonSessionStore.ReadAsync(
                    unityProject.RepositoryRoot,
                    unityProject.ProjectFingerprint,
                    token))
            .ConfigureAwait(false);
        if (!refreshedSessionRead.IsSuccess)
        {
            throw new TimeoutException(refreshedSessionRead.Error!.Message);
        }

        return refreshedSessionRead.Value!;
    }

    private async ValueTask<IpcPingResponse> PingWithinDeadlineAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        if (!deadline.TryGetRemainingTimeout(out var pingTimeout))
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
                    pingTimeout,
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
