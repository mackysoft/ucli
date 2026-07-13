using System.Net.Sockets;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Timing;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.UnityIntegration.Ipc.Recovery;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Features.Daemon.Common.Ipc;

/// <summary> Implements daemon IPC sending through persisted session endpoints with domain-reload recovery retry. </summary>
internal sealed class DaemonIpcRequestSender : IDaemonIpcRequestSender
{
    private const string DaemonSessionNotAvailableMessage = "No daemon session is available for the requested project. Start the daemon or check --projectPath.";

    private readonly IIpcTransportClient transportClient;

    private readonly IDaemonSessionConnectionProvider daemonSessionConnectionProvider;

    private readonly UnityDaemonRecoveryWaiter? recoveryWaiter;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="DaemonIpcRequestSender" /> class. </summary>
    public DaemonIpcRequestSender (
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
    public async ValueTask<DaemonIpcSendResult> SendAsync (
        ResolvedUnityProjectContext unityProject,
        Func<IpcSessionToken, IpcRequest> createRequest,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(createRequest);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        ExecutionDeadline? endpointAbsenceRetryDeadline = null;
        var sessionTokenRefreshAttempted = false;
        IpcSessionToken? rejectedSessionToken = null;
        IpcResponse? sessionTokenRejection = null;
        IpcRequest? request = null;

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
                var recoveryDelayConsumed = recoveryWaiter != null
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
                    return DaemonIpcSendResult.Failure(CreateDaemonSessionNotAvailableError());
                }

                return DaemonIpcSendResult.Failure(ExecutionError.InternalError(
                    $"Daemon session connection could not be resolved. {sessionConnectionResult.Error!.Message}"));
            }

            var isSessionTokenReplay = false;
            try
            {
                var sessionConnection = sessionConnectionResult.Connection!;
                if (rejectedSessionToken is not null
                    && sessionConnection.SessionToken.Equals(rejectedSessionToken))
                {
                    return DaemonIpcSendResult.Success(sessionTokenRejection!);
                }

                isSessionTokenReplay = rejectedSessionToken is not null;
                request ??= createRequest(sessionConnection.SessionToken);
                var requestForSession = sessionConnection.SessionToken.Matches(request.SessionToken)
                        ? request
                        : new IpcRequest(
                            request.ProtocolVersion,
                            request.RequestId,
                            sessionConnection.SessionToken.GetEncodedValue(),
                            request.Method,
                            request.Payload,
                            request.ResponseMode);
                var response = await transportClient.SendAsync(
                        sessionConnection.Endpoint,
                        requestForSession,
                        remainingTimeout,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (IsSessionTokenInvalid(response) && !sessionTokenRefreshAttempted)
                {
                    sessionTokenRefreshAttempted = true;
                    rejectedSessionToken = sessionConnection.SessionToken;
                    sessionTokenRejection = response;
                    if (!deadline.TryGetRemainingTimeout(out remainingTimeout))
                    {
                        return CreateDeadlineExceededResult(timeout);
                    }

                    await TimeProviderDelay.DelayAsync(
                            GetRetryDelay(remainingTimeout),
                            timeProvider,
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (!deadline.TryGetRemainingTimeout(out _))
                    {
                        return CreateDeadlineExceededResult(timeout);
                    }

                    continue;
                }

                return DaemonIpcSendResult.Success(response);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (TimeoutException)
            {
                return CreateDeadlineExceededResult(timeout);
            }
            catch (SocketException exception) when (DaemonEndpointAbsenceClassifier.IsDirectEndpointAbsence(exception))
            {
                if (isSessionTokenReplay)
                {
                    return DaemonIpcSendResult.Failure(CreateDaemonSessionNotAvailableError());
                }

                var retryDecision = await ShouldRetryEndpointAbsenceAsync(
                        unityProject,
                        deadline,
                        endpointAbsenceRetryDeadline,
                        cancellationToken)
                    .ConfigureAwait(false);
                endpointAbsenceRetryDeadline = retryDecision.EndpointAbsenceRetryDeadline;
                if (!retryDecision.ShouldRetry)
                {
                    if (deadline.IsExpired)
                    {
                        return CreateDeadlineExceededResult(timeout);
                    }

                    return DaemonIpcSendResult.Failure(CreateDaemonSessionNotAvailableError());
                }
            }
            catch (Exception exception) when (DaemonProbeExceptionClassifier.IsNotRunning(exception))
            {
                return DaemonIpcSendResult.Failure(CreateDaemonSessionNotAvailableError());
            }
            catch (Exception exception)
            {
                return DaemonIpcSendResult.Failure(ExecutionError.InternalError(
                    $"Daemon IPC request failed. {exception.Message}"));
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

    private static DaemonIpcSendResult CreateDeadlineExceededResult (TimeSpan timeout)
    {
        return DaemonIpcSendResult.Failure(ExecutionError.Timeout(
            $"Daemon IPC request timed out after {timeout.TotalMilliseconds:0} milliseconds."));
    }

    private static ExecutionError CreateDaemonSessionNotAvailableError ()
    {
        return ExecutionError.InternalError(
            DaemonSessionNotAvailableMessage,
            DaemonErrorCodes.DaemonSessionNotAvailable);
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

    private async ValueTask<(bool ShouldRetry, ExecutionDeadline? EndpointAbsenceRetryDeadline)> ShouldRetryEndpointAbsenceAsync (
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
        var retryDelayMilliseconds = Math.Min(
            DaemonTimeouts.StartupProbeRetryDelayMilliseconds,
            Math.Max(1, (int)Math.Ceiling(remainingTimeout.TotalMilliseconds)));
        return TimeSpan.FromMilliseconds(retryDelayMilliseconds);
    }
}
