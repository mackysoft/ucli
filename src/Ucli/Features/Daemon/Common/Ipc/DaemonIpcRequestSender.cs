using System.Net.Sockets;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Timing;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Recovery;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Features.Daemon.Common.Ipc;

/// <summary> Implements daemon IPC sending through persisted session endpoints with domain-reload recovery retry. </summary>
internal sealed class DaemonIpcRequestSender : IDaemonIpcRequestSender
{
    private readonly IIpcTransportClient transportClient;

    private readonly IDaemonSessionConnectionProvider daemonSessionConnectionProvider;

    private readonly UnityDaemonRecoveryWaiter? recoveryWaiter;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="DaemonIpcRequestSender" /> class. </summary>
    public DaemonIpcRequestSender (
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
    public async ValueTask<DaemonIpcSendResult> SendAsync (
        ResolvedUnityProjectContext unityProject,
        Func<string, IpcRequest> createRequest,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(createRequest);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        ExecutionDeadline? endpointAbsenceRetryDeadline = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
            {
                return DaemonIpcSendResult.Failure(ExecutionError.Timeout(
                    $"Daemon IPC request timed out after {timeout.TotalMilliseconds:0} milliseconds."));
            }

            var sessionConnectionResult = await daemonSessionConnectionProvider.ResolveAsync(
                    unityProject,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!sessionConnectionResult.IsSuccess)
            {
                if (recoveryWaiter != null
                    && await recoveryWaiter.DelayIfRecoveringAsync(unityProject, deadline, cancellationToken).ConfigureAwait(false))
                {
                    continue;
                }

                if (sessionConnectionResult.IsSessionNotAvailable)
                {
                    return DaemonIpcSendResult.Failure(ExecutionError.InternalError(
                        DaemonSessionConnectionResolutionResult.SessionNotAvailableMessage));
                }

                return DaemonIpcSendResult.Failure(ExecutionError.InternalError(
                    $"Daemon session connection could not be resolved. {sessionConnectionResult.Error!.Message}"));
            }

            try
            {
                var sessionConnection = sessionConnectionResult.Connection!;
                var response = await transportClient.SendAsync(
                        sessionConnection.Endpoint,
                        createRequest(sessionConnection.SessionToken),
                        remainingTimeout,
                        cancellationToken)
                    .ConfigureAwait(false);
                return DaemonIpcSendResult.Success(response);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (TimeoutException)
            {
                return DaemonIpcSendResult.Failure(ExecutionError.Timeout(
                    $"Daemon IPC request timed out after {timeout.TotalMilliseconds:0} milliseconds."));
            }
            catch (SocketException exception) when (DaemonEndpointAbsenceClassifier.IsDirectEndpointAbsence(exception))
            {
                var retryDecision = await ShouldRetryEndpointAbsenceAsync(
                        unityProject,
                        deadline,
                        endpointAbsenceRetryDeadline,
                        cancellationToken)
                    .ConfigureAwait(false);
                endpointAbsenceRetryDeadline = retryDecision.EndpointAbsenceRetryDeadline;
                if (!retryDecision.ShouldRetry)
                {
                    return DaemonIpcSendResult.Failure(ExecutionError.InternalError(
                        $"Unity daemon is not running. {exception.Message}"));
                }
            }
            catch (Exception exception) when (DaemonProbeExceptionClassifier.IsNotRunning(exception))
            {
                return DaemonIpcSendResult.Failure(ExecutionError.InternalError(
                    $"Unity daemon is not running. {exception.Message}"));
            }
            catch (Exception exception)
            {
                return DaemonIpcSendResult.Failure(ExecutionError.InternalError(
                    $"Daemon IPC request failed. {exception.Message}"));
            }
        }
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

        if (recoveryWaiter != null
            && await recoveryWaiter.DelayIfRecoveringAsync(unityProject, deadline, cancellationToken).ConfigureAwait(false))
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
