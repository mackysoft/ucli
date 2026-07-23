using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Shutdown;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Daemon.Common.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;
using MackySoft.Ucli.UnityIntegration.Ipc.Recovery;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Process.Shutdown;

/// <summary> Implements daemon shutdown request sending through Unity IPC client. </summary>
internal sealed class DaemonShutdownClient : IDaemonShutdownClient
{
    private readonly IIpcTransportClient transportClient;

    private readonly DaemonSessionAcquisitionCoordinator sessionAcquisitionCoordinator;

    /// <summary> Initializes a new instance of the <see cref="DaemonShutdownClient" /> class. </summary>
    /// <param name="transportClient"> The shared IPC transport client dependency. </param>
    /// <param name="sessionAcquisitionCoordinator"> The coordinator that creates one acquisition scope per logical request. </param>
    /// <exception cref="ArgumentNullException"> Thrown when a dependency is <see langword="null" />. </exception>
    public DaemonShutdownClient (
        IIpcTransportClient transportClient,
        DaemonSessionAcquisitionCoordinator sessionAcquisitionCoordinator)
    {
        this.transportClient = transportClient ?? throw new ArgumentNullException(nameof(transportClient));
        this.sessionAcquisitionCoordinator = sessionAcquisitionCoordinator ?? throw new ArgumentNullException(nameof(sessionAcquisitionCoordinator));
    }

    /// <summary> Sends one logical shutdown request across explicitly rejected session generations within the shared deadline. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="session"> The observed daemon session metadata used for the initial delivery. </param>
    /// <param name="deadline"> The deadline shared by the daemon-stop workflow. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The result of the accepted delivery or the terminal failure observed within the shared deadline. </returns>
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
        var currentSession = session;
        ExecutionError? sessionTokenRejectionError = null;
        DaemonSessionAcquisitionResult? sessionAcquisition = null;
        var acquisitionScope = sessionAcquisitionCoordinator.CreateScope(deadline);

        try
        {
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
                            return DaemonShutdownAttemptResult.Failure(ExecutionError.Timeout(
                                "Timed out while resolving a replacement daemon session for shutdown."));
                        case DaemonSessionAcquisitionKind.PublicationWindowExpired:
                            return DaemonShutdownAttemptResult.Failure(sessionTokenRejectionError!);
                        case DaemonSessionAcquisitionKind.EndpointAvailabilityWindowExpired:
                        case DaemonSessionAcquisitionKind.SessionNotAvailable:
                            return DaemonShutdownAttemptResult.NotRunning();
                        case DaemonSessionAcquisitionKind.SessionReadFailure:
                            return DaemonShutdownAttemptResult.Failure(ExecutionError.InternalError(
                                $"Daemon session could not be read for shutdown. {sessionAcquisition.ReadFailure!.Error!.Message}"));
                        default:
                            throw new InvalidOperationException(
                                $"Unsupported daemon session acquisition outcome: {sessionAcquisition.Kind}.");
                    }
                }

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
                    currentSession.SessionToken,
                    UnityIpcMethod.Shutdown,
                    payload,
                    requestId,
                    IpcResponseMode.Single,
                    deadline.UtcDeadline,
                    requestDeadlineRemainingMilliseconds);
                ExecutionDeadlineOperationResult<IpcResponse> sendOperation;
                try
                {
                    sendOperation = await ExecutionDeadlineOperation.ExecuteAsync(
                            deadline,
                            cancellationToken,
                            "Timed out before sending daemon shutdown request.",
                            "Timed out while sending daemon shutdown request.",
                            token => transportClient.SendAsync(
                                DaemonSessionIpcTransportEndpointAdapter.Adapt(currentSession),
                                request,
                                remainingTimeout,
                                token))
                        .ConfigureAwait(false);
                }
                catch (Exception exception) when (
                    DaemonIpcConnectionFailureClassifier.IsRetryableBeforeRequestWrite(exception))
                {
                    sessionAcquisition = await acquisitionScope.ResolveAfterPreWriteFailureAsync(
                            unityProject,
                            currentSession,
                            cancellationToken)
                        .ConfigureAwait(false);
                    continue;
                }

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
                    if (firstError.Code == IpcSessionErrorCodes.SessionTokenInvalid)
                    {
                        sessionTokenRejectionError = responseError;
                        sessionAcquisition = await acquisitionScope.ResolveReplacementAsync(
                                unityProject,
                                currentSession,
                                cancellationToken)
                            .ConfigureAwait(false);
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

}
