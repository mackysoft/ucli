using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityRequest;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;
using MackySoft.Ucli.UnityIntegration.Ipc.Failures;
using MackySoft.Ucli.UnityIntegration.Ipc.Recovery;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Clients;

/// <summary> Sends one IPC request through the running Unity daemon. </summary>
internal sealed class UnityDaemonIpcClient : IUnityIpcClient
{
    private readonly IUnityIpcTransportClient transportClient;

    private readonly IDaemonSessionTokenProvider daemonSessionTokenProvider;

    private readonly UnityDaemonRecoveryWaiter? recoveryWaiter;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="UnityDaemonIpcClient" /> class. </summary>
    /// <param name="transportClient"> The shared transport client dependency. </param>
    /// <param name="daemonSessionTokenProvider"> The daemon session-token provider dependency. </param>
    /// <param name="recoveryWaiter"> The daemon lifecycle recovery waiter dependency. </param>
    /// <param name="timeProvider"> The time provider used for retry deadlines and delays. </param>
    public UnityDaemonIpcClient (
        IUnityIpcTransportClient transportClient,
        IDaemonSessionTokenProvider daemonSessionTokenProvider,
        UnityDaemonRecoveryWaiter? recoveryWaiter = null,
        TimeProvider? timeProvider = null)
    {
        this.transportClient = transportClient ?? throw new ArgumentNullException(nameof(transportClient));
        this.daemonSessionTokenProvider = daemonSessionTokenProvider ?? throw new ArgumentNullException(nameof(daemonSessionTokenProvider));
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
        var requestId = UnityIpcRequestFactory.CreateRequestId(dispatchRequest.Method);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
            {
                return UnityRequestExecutionResult.Failure(UnityIpcFailureClassifier.Timeout(
                    $"Unity daemon IPC request timed out after {timeout.TotalMilliseconds:0} milliseconds."));
            }

            var sessionTokenResult = await daemonSessionTokenProvider.ResolveAsync(unityProject, cancellationToken).ConfigureAwait(false);
            if (!sessionTokenResult.IsSuccess)
            {
                if (dispatchRequest.IsRecoverable
                    && recoveryWaiter != null
                    && await recoveryWaiter.DelayIfRecoveringAsync(unityProject, deadline, cancellationToken).ConfigureAwait(false))
                {
                    continue;
                }

                var message = sessionTokenResult.IsSessionNotAvailable
                    ? "Daemon session token is not available."
                    : $"Daemon session token could not be resolved. {sessionTokenResult.Error!.Message}";
                return UnityRequestExecutionResult.Failure(UnityIpcFailureClassifier.InternalError(message));
            }

            try
            {
                var response = await transportClient.SendAsync(
                        unityProject.RepositoryRoot,
                        unityProject.ProjectFingerprint,
                        UnityIpcRequestFactory.Create(
                            sessionTokenResult.Token!,
                            dispatchRequest.Method,
                            dispatchRequest.Payload,
                            requestId,
                            remainingTimeout),
                        remainingTimeout,
                        cancellationToken)
                    .ConfigureAwait(false);
                return UnityRequestExecutionResult.Success(UnityRequestResponseFactory.Create(response));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                if (dispatchRequest.IsRecoverable
                    && await ShouldRetryRecoverableDispatchAsync(unityProject, deadline, exception, cancellationToken).ConfigureAwait(false))
                {
                    continue;
                }

                return UnityRequestExecutionResult.Failure(
                    UnityIpcFailureClassifier.FromDaemonDispatchException(exception, remainingTimeout));
            }
        }
    }

    private async ValueTask<bool> ShouldRetryRecoverableDispatchAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
        {
            return false;
        }

        if (recoveryWaiter != null
            && await recoveryWaiter.DelayIfRecoveringAsync(unityProject, deadline, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        if (!IsResponseLossOrTimeout(exception))
        {
            return false;
        }

        await TimeProviderDelay.DelayAsync(
                GetRetryDelay(remainingTimeout),
                timeProvider,
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static bool IsResponseLossOrTimeout (Exception exception)
    {
        return exception is TimeoutException or EndOfStreamException or IOException or ObjectDisposedException or InvalidOperationException;
    }

    private static TimeSpan GetRetryDelay (TimeSpan remainingTimeout)
    {
        var retryDelayMilliseconds = Math.Min(100, Math.Max(1, (int)Math.Ceiling(remainingTimeout.TotalMilliseconds)));
        return TimeSpan.FromMilliseconds(retryDelayMilliseconds);
    }
}
