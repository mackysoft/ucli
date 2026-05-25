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
        UnityDaemonRecoveryWaiter? recoveryWaiter = null,
        TimeProvider? timeProvider = null)
    {
        this.transportClient = transportClient ?? throw new ArgumentNullException(nameof(transportClient));
        this.daemonSessionConnectionProvider = daemonSessionConnectionProvider ?? throw new ArgumentNullException(nameof(daemonSessionConnectionProvider));
        this.recoveryWaiter = recoveryWaiter;
        this.timeProvider = timeProvider ?? TimeProvider.System;
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
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(dispatchRequest);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        // NOTE: Recoverable replay must keep one requestId so Unity can find the same
        // operation record after domain reload. The session token is still resolved on
        // every attempt because the daemon endpoint can be re-registered.
        var requestId = UnityIpcRequestFactory.CreateRequestId(dispatchRequest.Method);
        ExecutionDeadline? endpointAbsenceRetryDeadline = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
            {
                return UnityRequestExecutionResult.Failure(UnityIpcFailureClassifier.Timeout(
                    $"Unity daemon IPC request timed out after {timeout.TotalMilliseconds:0} milliseconds."));
            }

            var sessionConnectionResult = await daemonSessionConnectionProvider.ResolveAsync(unityProject, cancellationToken).ConfigureAwait(false);
            if (!sessionConnectionResult.IsSuccess)
            {
                if (dispatchRequest.IsRecoverable
                    && recoveryWaiter != null
                    && await recoveryWaiter.DelayIfRecoveringAsync(unityProject, deadline, cancellationToken).ConfigureAwait(false))
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

            try
            {
                var sessionConnection = sessionConnectionResult.Connection!;
                var attemptTimeout = ResolveAttemptTimeout(dispatchRequest, remainingTimeout);
                var response = await transportClient.SendAsync(
                        sessionConnection.Endpoint,
                        UnityIpcRequestFactory.Create(
                            sessionConnection.SessionToken,
                            dispatchRequest,
                            requestId,
                            remainingTimeout),
                        attemptTimeout,
                        cancellationToken)
                    .ConfigureAwait(false);
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
                    if (retryDecision.ShouldRetry)
                    {
                        continue;
                    }
                }

                return UnityRequestExecutionResult.Failure(
                    UnityIpcFailureClassifier.FromDaemonDispatchException(exception, remainingTimeout));
            }
        }
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
                onProgressFrame,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<UnityRequestExecutionResult> SendCoreAsync (
        ResolvedUnityProjectContext unityProject,
        UnityIpcDispatchRequest dispatchRequest,
        TimeSpan timeout,
        Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(dispatchRequest);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        var requestId = UnityIpcRequestFactory.CreateRequestId(dispatchRequest.Method);
        ExecutionDeadline? endpointAbsenceRetryDeadline = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
            {
                return UnityRequestExecutionResult.Failure(UnityIpcFailureClassifier.Timeout(
                    $"Unity daemon IPC request timed out after {timeout.TotalMilliseconds:0} milliseconds."));
            }

            var sessionConnectionResult = await daemonSessionConnectionProvider.ResolveAsync(unityProject, cancellationToken).ConfigureAwait(false);
            if (!sessionConnectionResult.IsSuccess)
            {
                if (recoveryWaiter != null
                    && await recoveryWaiter.DelayIfRecoveringAsync(unityProject, deadline, cancellationToken).ConfigureAwait(false))
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

            try
            {
                var sessionConnection = sessionConnectionResult.Connection!;
                var attemptTimeout = ResolveAttemptTimeout(dispatchRequest, remainingTimeout);
                var response = await transportClient.SendStreamingAsync(
                        sessionConnection.Endpoint,
                        UnityIpcRequestFactory.Create(
                            sessionConnection.SessionToken,
                            dispatchRequest,
                            requestId,
                            remainingTimeout),
                        attemptTimeout,
                        onProgressFrame,
                        cancellationToken)
                    .ConfigureAwait(false);
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
                var retryDecision = await ShouldRetryEndpointAbsenceAsync(
                        unityProject,
                        deadline,
                        endpointAbsenceRetryDeadline,
                        exception,
                        cancellationToken)
                    .ConfigureAwait(false);
                endpointAbsenceRetryDeadline = retryDecision.EndpointAbsenceRetryDeadline;
                if (retryDecision.ShouldRetry)
                {
                    continue;
                }

                return UnityRequestExecutionResult.Failure(
                    UnityIpcFailureClassifier.FromDaemonDispatchException(exception, remainingTimeout));
            }
        }
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

        if (recoveryWaiter != null
            && await recoveryWaiter.DelayIfRecoveringAsync(unityProject, deadline, cancellationToken).ConfigureAwait(false))
        {
            return (true, endpointAbsenceRetryDeadline);
        }

        if (IsRecoverableEndpointAbsence(exception))
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

    private async ValueTask<(bool ShouldRetry, ExecutionDeadline? EndpointAbsenceRetryDeadline)> ShouldRetryEndpointAbsenceAsync (
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

        if (!IsRecoverableEndpointAbsence(exception))
        {
            return (false, endpointAbsenceRetryDeadline);
        }

        if (recoveryWaiter != null
            && await recoveryWaiter.DelayIfRecoveringAsync(
                unityProject,
                deadline,
                cancellationToken).ConfigureAwait(false))
        {
            return (true, endpointAbsenceRetryDeadline);
        }

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

    private static bool IsRecoverableResponseInterruption (Exception exception)
    {
        return exception is TimeoutException or EndOfStreamException or IOException or ObjectDisposedException or InvalidOperationException;
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
