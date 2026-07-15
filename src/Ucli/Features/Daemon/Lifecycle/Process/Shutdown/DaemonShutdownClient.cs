using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Shutdown;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Timing;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;
using MackySoft.Ucli.UnityIntegration.Ipc.Recovery;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Process.Shutdown;

/// <summary> Implements daemon shutdown request sending through Unity IPC client. </summary>
internal sealed class DaemonShutdownClient : IDaemonShutdownClient
{
    private readonly IIpcTransportClient transportClient;

    private readonly IDaemonSessionConnectionProvider daemonSessionConnectionProvider;

    private readonly UnityDaemonRecoveryWaiter? recoveryWaiter;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="DaemonShutdownClient" /> class. </summary>
    /// <param name="transportClient"> The shared IPC transport client dependency. </param>
    /// <param name="daemonSessionConnectionProvider"> The provider used to observe one replacement session after token rejection. </param>
    /// <param name="recoveryWaiter"> The optional domain-reload recovery waiter. </param>
    /// <param name="timeProvider"> The time provider used for bounded session-publication retry. </param>
    /// <exception cref="ArgumentNullException"> Thrown when a dependency is <see langword="null" />. </exception>
    public DaemonShutdownClient (
        IIpcTransportClient transportClient,
        IDaemonSessionConnectionProvider daemonSessionConnectionProvider,
        UnityDaemonRecoveryWaiter? recoveryWaiter,
        TimeProvider timeProvider)
    {
        this.transportClient = transportClient ?? throw new ArgumentNullException(nameof(transportClient));
        this.daemonSessionConnectionProvider = daemonSessionConnectionProvider ?? throw new ArgumentNullException(nameof(daemonSessionConnectionProvider));
        this.recoveryWaiter = recoveryWaiter;
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary> Sends one logical shutdown request and follows at most one replacement session after explicit token rejection. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="session"> The observed daemon session metadata used for the initial delivery. </param>
    /// <param name="deadline"> The deadline shared by the daemon-stop workflow. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The result of the initial delivery or the single permitted replacement delivery. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" />, <paramref name="session" />, or <paramref name="deadline" /> is <see langword="null" />. </exception>
    public async ValueTask<DaemonShutdownAttemptResult> SendShutdownAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(deadline);

        var payload = IpcPayloadCodec.SerializeToElement(new IpcShutdownRequest("ucli-daemon-stop"));
        var requestId = Guid.NewGuid();
        var sessionConnection = new DaemonSessionConnection(session.SessionToken, session.Endpoint);
        var followedReplacementSession = false;

        try
        {
            while (true)
            {
                if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
                {
                    return DaemonShutdownAttemptResult.Failure(ExecutionError.Timeout(
                        "Timed out before sending daemon shutdown request."));
                }

                if (!deadline.TryGetRemainingMilliseconds(out var requestDeadlineRemainingMilliseconds))
                {
                    return DaemonShutdownAttemptResult.Failure(ExecutionError.Timeout(
                        "Timed out before sending daemon shutdown request."));
                }

                var request = UnityIpcRequestFactory.Create(
                    sessionConnection.SessionToken,
                    UnityIpcMethod.Shutdown,
                    payload,
                    requestId,
                    IpcResponseMode.Single,
                    deadline.UtcDeadline,
                    requestDeadlineRemainingMilliseconds);
                var sendOperation = await ExecutionDeadlineOperation.ExecuteAsync(
                        deadline,
                        cancellationToken,
                        "Timed out before sending daemon shutdown request.",
                        "Timed out while sending daemon shutdown request.",
                        token => transportClient.SendAsync(
                            sessionConnection.Endpoint,
                            request,
                            remainingTimeout,
                            token))
                    .ConfigureAwait(false);
                if (!sendOperation.IsSuccess)
                {
                    return DaemonShutdownAttemptResult.Failure(sendOperation.Error!);
                }

                var response = sendOperation.Value!;

                if (IpcResponseFailureReader.TryRead(response, out var firstError))
                {
                    var responseError = DaemonProbeExceptionClassifier.IsSessionAuthenticationRejected(firstError.Code)
                        ? ExecutionError.InternalError(
                            $"Daemon shutdown request was rejected by session authentication. ErrorCode='{firstError.Code}'.")
                        : ExecutionError.InternalError(
                            $"Daemon shutdown request failed with error code '{firstError.Code}'.");
                    if (firstError.Code == IpcSessionErrorCodes.SessionTokenInvalid
                        && !followedReplacementSession)
                    {
                        followedReplacementSession = true;
                        var replacementSessionResult = await ResolveDistinctReplacementSessionAsync(
                                unityProject,
                                sessionConnection.SessionToken,
                                responseError,
                                deadline,
                                cancellationToken)
                            .ConfigureAwait(false);
                        if (!replacementSessionResult.IsSuccess)
                        {
                            return DaemonShutdownAttemptResult.Failure(replacementSessionResult.Error!);
                        }

                        sessionConnection = replacementSessionResult.Connection!;
                        continue;
                    }

                    return DaemonShutdownAttemptResult.Failure(responseError);
                }

                return DaemonShutdownAttemptResult.Success();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException exception)
        {
            return DaemonShutdownAttemptResult.Failure(ExecutionError.Timeout(
                $"Timed out while sending daemon shutdown request. {exception.Message}"));
        }
        catch (IpcConnectException exception) when (DaemonEndpointAbsenceClassifier.IsDirectEndpointAbsence(exception))
        {
            return DaemonShutdownAttemptResult.NotRunning();
        }
        catch (Exception exception)
        {
            return DaemonShutdownAttemptResult.Failure(ExecutionError.InternalError(
                $"Failed to send daemon shutdown request. {exception.Message}"));
        }
    }

    private async ValueTask<DaemonSessionConnectionResolutionResult> ResolveDistinctReplacementSessionAsync (
        ResolvedUnityProjectContext unityProject,
        IpcSessionToken rejectedSessionToken,
        ExecutionError sessionTokenRejectionError,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        ExecutionDeadline? sessionPublicationRetryDeadline = null;
        while (true)
        {
            var publicationRetryDecision = await DelayForSessionPublicationAsync(
                    unityProject,
                    deadline,
                    sessionPublicationRetryDeadline,
                    cancellationToken)
                .ConfigureAwait(false);
            sessionPublicationRetryDeadline = publicationRetryDecision.SessionPublicationRetryDeadline;
            if (!publicationRetryDecision.ShouldRetry)
            {
                return deadline.IsExpired
                    ? DaemonSessionConnectionResolutionResult.Failure(ExecutionError.Timeout(
                        "Timed out while waiting for replacement daemon session publication before shutdown."))
                    : DaemonSessionConnectionResolutionResult.Failure(sessionTokenRejectionError);
            }

            var sessionConnectionResolution = await ResolveSessionConnectionBeforeDeadlineAsync(
                    unityProject,
                    deadline,
                    cancellationToken)
                .ConfigureAwait(false);
            if (sessionConnectionResolution.DeadlineExpired)
            {
                return DaemonSessionConnectionResolutionResult.Failure(ExecutionError.Timeout(
                    "Timed out while resolving a replacement daemon session for shutdown."));
            }

            var sessionConnectionResult = sessionConnectionResolution.ConnectionResult!;
            if (sessionConnectionResult.IsSuccess)
            {
                if (!sessionConnectionResult.Connection!.SessionToken.Equals(rejectedSessionToken))
                {
                    return sessionConnectionResult;
                }

                continue;
            }

            if (sessionConnectionResult.IsSessionNotAvailable)
            {
                continue;
            }

            return DaemonSessionConnectionResolutionResult.Failure(ExecutionError.InternalError(
                $"Daemon session connection could not be resolved for shutdown. {sessionConnectionResult.Error!.Message}"));
        }
    }

    private async ValueTask<(DaemonSessionConnectionResolutionResult? ConnectionResult, bool DeadlineExpired)> ResolveSessionConnectionBeforeDeadlineAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        var resolutionOperation = await ExecutionDeadlineOperation.ExecuteAsync(
                deadline,
                cancellationToken,
                "Timed out before replacement daemon session resolution could begin.",
                "Timed out while resolving a replacement daemon session for shutdown.",
                token => daemonSessionConnectionProvider.ResolveAsync(unityProject, token))
            .ConfigureAwait(false);
        if (!resolutionOperation.IsSuccess)
        {
            return (null, true);
        }

        return (resolutionOperation.Value!, false);
    }

    private async ValueTask<(bool ShouldRetry, ExecutionDeadline? SessionPublicationRetryDeadline)> DelayForSessionPublicationAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        ExecutionDeadline? sessionPublicationRetryDeadline,
        CancellationToken cancellationToken)
    {
        if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
        {
            return (false, sessionPublicationRetryDeadline);
        }

        var effectivePublicationRetryDeadline = sessionPublicationRetryDeadline
            ?? deadline.CreateCappedDeadline(DaemonTimeouts.SessionPublicationRetryTimeout);
        if (!effectivePublicationRetryDeadline.TryGetRemainingTimeout(out var publicationRemainingTimeout))
        {
            return (false, effectivePublicationRetryDeadline);
        }

        var recoveryDelayConsumed = recoveryWaiter != null
            && await recoveryWaiter.DelayIfRecoveringAsync(unityProject, effectivePublicationRetryDeadline, cancellationToken).ConfigureAwait(false);
        if (!deadline.TryGetRemainingTimeout(out remainingTimeout)
            || !effectivePublicationRetryDeadline.TryGetRemainingTimeout(out publicationRemainingTimeout))
        {
            return (false, effectivePublicationRetryDeadline);
        }

        if (recoveryDelayConsumed)
        {
            return (true, effectivePublicationRetryDeadline);
        }

        await TimeProviderDelay.DelayAsync(
                GetRetryDelay(GetShorterTimeout(remainingTimeout, publicationRemainingTimeout)),
                timeProvider,
                cancellationToken)
            .ConfigureAwait(false);
        return (true, effectivePublicationRetryDeadline);
    }

    private static TimeSpan GetShorterTimeout (
        TimeSpan first,
        TimeSpan second)
    {
        return first < second ? first : second;
    }

    private static TimeSpan GetRetryDelay (TimeSpan remainingTimeout)
    {
        var retryDelayMilliseconds = Math.Min(
            DaemonTimeouts.StartupProbeRetryDelayMilliseconds,
            Math.Max(1, (int)Math.Ceiling(remainingTimeout.TotalMilliseconds)));
        return TimeSpan.FromMilliseconds(retryDelayMilliseconds);
    }
}
