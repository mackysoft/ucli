using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Timing;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityRequest;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;
using MackySoft.Ucli.UnityIntegration.Ipc.Failures;
using MackySoft.Ucli.UnityIntegration.Ipc.Recovery;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Clients;

/// <summary> Sends one IPC request through the running Unity daemon. </summary>
internal sealed class UnityDaemonIpcClient : IUnityIpcClient
{
    private const int TerminalResponseGraceDivisor = 10;

    private static readonly TimeSpan MaximumTerminalResponseGrace = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan MinimumServerExecutionTimeout = TimeSpan.FromMilliseconds(1);

    private readonly IIpcTransportClient transportClient;

    private readonly IDaemonSessionConnectionProvider daemonSessionConnectionProvider;

    private readonly UnityDaemonRecoveryWaiter? recoveryWaiter;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="UnityDaemonIpcClient" /> class. </summary>
    /// <param name="transportClient"> The shared transport client dependency. </param>
    /// <param name="daemonSessionConnectionProvider"> The daemon session connection provider dependency. </param>
    /// <param name="recoveryWaiter"> The daemon lifecycle recovery waiter dependency. </param>
    /// <param name="timeProvider"> The time provider used for retry deadlines and delays. </param>
    public UnityDaemonIpcClient (
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

    /// <inheritdoc />
    public UnityExecutionTarget Target => UnityExecutionTarget.Daemon;

    /// <inheritdoc />
    public async ValueTask<UnityRequestExecutionResult> SendAsync (
        ResolvedUnityProjectContext unityProject,
        UnityIpcDispatchRequest dispatchRequest,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(dispatchRequest);
        return await SendCoreAsync(
                unityProject,
                dispatchRequest,
                timeout,
                allowEndpointRecovery: dispatchRequest.IsRecoverable,
                (endpoint, request, attemptTimeout, token) => transportClient.SendAsync(
                    endpoint,
                    request,
                    attemptTimeout,
                    token),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<UnityRequestExecutionResult> SendStreamingAsync (
        ResolvedUnityProjectContext unityProject,
        UnityIpcDispatchRequest dispatchRequest,
        TimeSpan timeout,
        Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onProgressFrame);
        ArgumentNullException.ThrowIfNull(dispatchRequest);
        if (dispatchRequest.IsRecoverable)
        {
            return UnityRequestExecutionResult.Failure(UnityIpcFailureClassifier.InternalError(
                $"Streaming IPC dispatch does not support recoverable request replay: {dispatchRequest.Method}."));
        }

        return await SendCoreAsync(
                unityProject,
                dispatchRequest,
                timeout,
                allowEndpointRecovery: true,
                (endpoint, request, attemptTimeout, token) => transportClient.SendStreamingAsync(
                    endpoint,
                    request,
                    attemptTimeout,
                    onProgressFrame,
                    token),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<UnityRequestExecutionResult> SendCoreAsync (
        ResolvedUnityProjectContext unityProject,
        UnityIpcDispatchRequest dispatchRequest,
        TimeSpan timeout,
        bool allowEndpointRecovery,
        Func<IpcEndpoint, IpcRequest, TimeSpan, CancellationToken, ValueTask<IpcResponse>> sendAttempt,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        var requestId = Guid.NewGuid();
        ExecutionDeadline? endpointAbsenceRetryDeadline = null;
        var sessionTokenRefreshAttempted = false;
        string? rejectedSessionToken = null;
        IpcResponse? sessionTokenRejection = null;

        while (true)
        {
            var sessionConnectionResolution = await ResolveSessionConnectionBeforeDeadlineAsync(
                    unityProject,
                    deadline,
                    cancellationToken)
                .ConfigureAwait(false);
            if (sessionConnectionResolution.DeadlineExpired)
            {
                return CreateDeadlineExceededResult(timeout);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
            {
                return CreateDeadlineExceededResult(timeout);
            }

            var sessionConnectionResult = sessionConnectionResolution.ConnectionResult!;
            if (!sessionConnectionResult.IsSuccess)
            {
                var recoveryDelayConsumed = allowEndpointRecovery
                    && recoveryWaiter != null
                    && await recoveryWaiter.DelayIfRecoveringAsync(unityProject, deadline, cancellationToken).ConfigureAwait(false);

                if (!deadline.TryGetRemainingTimeout(out remainingTimeout))
                {
                    return CreateDeadlineExceededResult(timeout);
                }

                if (recoveryDelayConsumed)
                {
                    continue;
                }

                if (sessionConnectionResult.IsSessionNotAvailable)
                {
                    return UnityRequestExecutionResult.Failure(UnityIpcFailureClassifier.FromCodeAndMessage(
                        UnityExecutionModeDecisionErrorCodes.DaemonNotRunning,
                        DaemonSessionConnectionResolutionResult.SessionNotAvailableMessage));
                }

                return UnityRequestExecutionResult.Failure(UnityIpcFailureClassifier.InternalError(
                    $"Daemon session connection could not be resolved. {sessionConnectionResult.Error!.Message}"));
            }

            var isSessionTokenReplayAttempt = false;
            try
            {
                var sessionConnection = sessionConnectionResult.Connection!;
                if (rejectedSessionToken is not null
                    && string.Equals(
                        sessionConnection.SessionToken,
                        rejectedSessionToken,
                        StringComparison.Ordinal))
                {
                    return UnityRequestExecutionResult.Success(
                        UnityRequestResponseFactory.Create(sessionTokenRejection!));
                }

                isSessionTokenReplayAttempt = rejectedSessionToken is not null;

                var attemptTimeout = ResolveAttemptTimeout(dispatchRequest, remainingTimeout);
                var serverExecutionTimeout = ResolveServerExecutionTimeout(attemptTimeout);
                var response = await sendAttempt(
                        sessionConnection.Endpoint,
                        UnityIpcRequestFactory.Create(
                            sessionConnection.SessionToken,
                            dispatchRequest.Method,
                            dispatchRequest.CreatePayload(serverExecutionTimeout),
                            requestId,
                            dispatchRequest.ResponseMode),
                        attemptTimeout,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (IsSessionTokenInvalid(response) && !sessionTokenRefreshAttempted)
                {
                    sessionTokenRefreshAttempted = true;
                    rejectedSessionToken = sessionConnection.SessionToken;
                    sessionTokenRejection = response;
                    var retryDecision = await DelayForSessionRotationOrEndpointAvailabilityAsync(
                            unityProject,
                            deadline,
                            endpointAbsenceRetryDeadline,
                            cancellationToken)
                        .ConfigureAwait(false);
                    endpointAbsenceRetryDeadline = retryDecision.EndpointAbsenceRetryDeadline;
                    if (retryDecision.ShouldRetry)
                    {
                        continue;
                    }

                    if (deadline.IsExpired)
                    {
                        return CreateDeadlineExceededResult(timeout);
                    }
                }

                return UnityRequestExecutionResult.Success(UnityRequestResponseFactory.Create(response));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (IpcProgressFrameHandlerException exception)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException!).Throw();
                throw;
            }
            catch (Exception exception)
            {
                if (isSessionTokenReplayAttempt)
                {
                    if (deadline.IsExpired)
                    {
                        return CreateDeadlineExceededResult(timeout);
                    }

                    return UnityRequestExecutionResult.Failure(
                        UnityIpcFailureClassifier.FromDaemonDispatchException(exception, remainingTimeout));
                }

                var shouldRetry = false;
                if (dispatchRequest.IsRecoverable)
                {
                    var retryDecision = await ShouldRetryRecoverableDispatchAsync(
                            unityProject,
                            deadline,
                            endpointAbsenceRetryDeadline,
                            exception,
                            cancellationToken)
                        .ConfigureAwait(false);
                    endpointAbsenceRetryDeadline = retryDecision.EndpointAbsenceRetryDeadline;
                    shouldRetry = retryDecision.ShouldRetry;
                }
                else if (allowEndpointRecovery)
                {
                    var retryDecision = await ShouldRetryEndpointAbsenceAsync(
                            unityProject,
                            deadline,
                            endpointAbsenceRetryDeadline,
                            exception,
                            cancellationToken)
                        .ConfigureAwait(false);
                    endpointAbsenceRetryDeadline = retryDecision.EndpointAbsenceRetryDeadline;
                    shouldRetry = retryDecision.ShouldRetry;
                }

                if (shouldRetry)
                {
                    continue;
                }

                if (deadline.IsExpired)
                {
                    return CreateDeadlineExceededResult(timeout);
                }

                return UnityRequestExecutionResult.Failure(
                    UnityIpcFailureClassifier.FromDaemonDispatchException(exception, remainingTimeout));
            }
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
                "Timed out before daemon session resolution could begin.",
                "Timed out while resolving daemon session.",
                token => daemonSessionConnectionProvider.ResolveAsync(unityProject, token))
            .ConfigureAwait(false);
        if (!resolutionOperation.IsSuccess)
        {
            return (null, true);
        }

        return (resolutionOperation.Value!, false);
    }

    private static UnityRequestExecutionResult CreateDeadlineExceededResult (TimeSpan timeout)
    {
        return UnityRequestExecutionResult.Failure(UnityIpcFailureClassifier.Timeout(
            $"Unity daemon IPC request timed out after {timeout.TotalMilliseconds:0} milliseconds."));
    }

    private async ValueTask<(bool ShouldRetry, ExecutionDeadline? EndpointAbsenceRetryDeadline)> ShouldRetryRecoverableDispatchAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        ExecutionDeadline? endpointAbsenceRetryDeadline,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
        {
            return (false, endpointAbsenceRetryDeadline);
        }

        var recoveryDelayConsumed = recoveryWaiter != null
            && await recoveryWaiter.DelayIfRecoveringAsync(unityProject, deadline, cancellationToken).ConfigureAwait(false);
        if (!deadline.TryGetRemainingTimeout(out remainingTimeout))
        {
            return (false, endpointAbsenceRetryDeadline);
        }

        if (recoveryDelayConsumed)
        {
            return (true, endpointAbsenceRetryDeadline);
        }

        if (IsRecoverableEndpointAbsence(exception))
        {
            return await DelayWithinEndpointAbsenceGraceAsync(
                    remainingTimeout,
                    endpointAbsenceRetryDeadline,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (!IsRecoverableResponseInterruption(exception))
        {
            return (false, endpointAbsenceRetryDeadline);
        }

        await TimeProviderDelay.DelayAsync(
                GetRetryDelay(remainingTimeout),
                timeProvider,
                cancellationToken)
            .ConfigureAwait(false);
        return (true, endpointAbsenceRetryDeadline);
    }

    private async ValueTask<(bool ShouldRetry, ExecutionDeadline? EndpointAbsenceRetryDeadline)> DelayForSessionRotationOrEndpointAvailabilityAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        ExecutionDeadline? endpointAbsenceRetryDeadline,
        CancellationToken cancellationToken)
    {
        if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
        {
            return (false, endpointAbsenceRetryDeadline);
        }

        var recoveryDelayConsumed = recoveryWaiter != null
            && await recoveryWaiter.DelayIfRecoveringAsync(unityProject, deadline, cancellationToken).ConfigureAwait(false);
        if (!deadline.TryGetRemainingTimeout(out remainingTimeout))
        {
            return (false, endpointAbsenceRetryDeadline);
        }

        if (recoveryDelayConsumed)
        {
            return (true, endpointAbsenceRetryDeadline);
        }

        return await DelayWithinEndpointAbsenceGraceAsync(
                remainingTimeout,
                endpointAbsenceRetryDeadline,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<(bool ShouldRetry, ExecutionDeadline? EndpointAbsenceRetryDeadline)> DelayWithinEndpointAbsenceGraceAsync (
        TimeSpan remainingTimeout,
        ExecutionDeadline? endpointAbsenceRetryDeadline,
        CancellationToken cancellationToken)
    {
        var effectiveEndpointAbsenceRetryDeadline = endpointAbsenceRetryDeadline
            ?? ExecutionDeadline.Start(GetEndpointAbsenceRetryTimeout(remainingTimeout), timeProvider);
        if (!effectiveEndpointAbsenceRetryDeadline.TryGetRemainingTimeout(out var endpointAbsenceRemainingTimeout))
        {
            return (false, effectiveEndpointAbsenceRetryDeadline);
        }

        await TimeProviderDelay.DelayAsync(
                GetRetryDelay(GetShorterTimeout(remainingTimeout, endpointAbsenceRemainingTimeout)),
                timeProvider,
                cancellationToken)
            .ConfigureAwait(false);
        return (true, effectiveEndpointAbsenceRetryDeadline);
    }

    private async ValueTask<(bool ShouldRetry, ExecutionDeadline? EndpointAbsenceRetryDeadline)> ShouldRetryEndpointAbsenceAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        ExecutionDeadline? endpointAbsenceRetryDeadline,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (!IsRecoverableEndpointAbsence(exception))
        {
            return (false, endpointAbsenceRetryDeadline);
        }

        return await DelayForSessionRotationOrEndpointAvailabilityAsync(
                unityProject,
                deadline,
                endpointAbsenceRetryDeadline,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool IsRecoverableResponseInterruption (Exception exception)
    {
        return exception is TimeoutException or EndOfStreamException or IOException or ObjectDisposedException or InvalidOperationException;
    }

    private static bool IsSessionTokenInvalid (IpcResponse response)
    {
        foreach (var error in response.Errors)
        {
            if (error.Code == IpcSessionErrorCodes.SessionTokenInvalid)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsRecoverableEndpointAbsence (Exception exception)
    {
        return exception is SocketException socketException
            && DaemonEndpointAbsenceClassifier.IsDirectEndpointAbsence(socketException);
    }

    private static TimeSpan ResolveAttemptTimeout (
        UnityIpcDispatchRequest dispatchRequest,
        TimeSpan remainingTimeout)
    {
        if (!dispatchRequest.IsRecoverable
            || !dispatchRequest.RecoverableResponseAttemptTimeout.HasValue
            || dispatchRequest.RecoverableResponseAttemptTimeout.Value >= remainingTimeout)
        {
            return remainingTimeout;
        }

        return dispatchRequest.RecoverableResponseAttemptTimeout.Value;
    }

    private static TimeSpan ResolveServerExecutionTimeout (TimeSpan transportTimeout)
    {
        if (transportTimeout <= MinimumServerExecutionTimeout)
        {
            return transportTimeout;
        }

        var proportionalGrace = TimeSpan.FromTicks(Math.Max(
            MinimumServerExecutionTimeout.Ticks,
            transportTimeout.Ticks / TerminalResponseGraceDivisor));
        var maximumAllowedGrace = transportTimeout - MinimumServerExecutionTimeout;
        var responseGrace = GetShorterTimeout(
            GetShorterTimeout(proportionalGrace, MaximumTerminalResponseGrace),
            maximumAllowedGrace);
        return transportTimeout - responseGrace;
    }

    private static TimeSpan GetEndpointAbsenceRetryTimeout (TimeSpan remainingTimeout)
    {
        return GetShorterTimeout(remainingTimeout, DaemonTimeouts.ProbeAttemptTimeoutCap);
    }

    private static TimeSpan GetShorterTimeout (
        TimeSpan first,
        TimeSpan second)
    {
        return first < second ? first : second;
    }

    private static TimeSpan GetRetryDelay (TimeSpan remainingTimeout)
    {
        var retryDelayMilliseconds = Math.Min(100, Math.Max(1, (int)Math.Ceiling(remainingTimeout.TotalMilliseconds)));
        return TimeSpan.FromMilliseconds(retryDelayMilliseconds);
    }
}
