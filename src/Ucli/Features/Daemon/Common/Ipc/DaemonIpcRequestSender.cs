using System.Text.Json;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Reachability;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Features.Daemon.Common.Ipc;

/// <summary> Implements daemon IPC sending through persisted session endpoints with domain-reload recovery retry. </summary>
internal sealed class DaemonIpcRequestSender : IDaemonIpcRequestSender
{
    private const string DaemonSessionNotAvailableMessage = "No daemon session is available for the requested project. Start the daemon or check --projectPath.";

    private readonly IIpcTransportClient transportClient;

    private readonly DaemonSessionAcquisitionCoordinator sessionAcquisitionCoordinator;

    private readonly IDaemonReachabilityClassifier reachabilityClassifier;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="DaemonIpcRequestSender" /> class. </summary>
    public DaemonIpcRequestSender (
        IIpcTransportClient transportClient,
        DaemonSessionAcquisitionCoordinator sessionAcquisitionCoordinator,
        IDaemonReachabilityClassifier reachabilityClassifier,
        TimeProvider timeProvider)
    {
        this.transportClient = transportClient ?? throw new ArgumentNullException(nameof(transportClient));
        this.sessionAcquisitionCoordinator = sessionAcquisitionCoordinator ?? throw new ArgumentNullException(nameof(sessionAcquisitionCoordinator));
        this.reachabilityClassifier = reachabilityClassifier ?? throw new ArgumentNullException(nameof(reachabilityClassifier));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc />
    public async ValueTask<DaemonIpcSendResult> SendAsync (
        ResolvedUnityProjectContext unityProject,
        UnityIpcMethod method,
        JsonElement payload,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        if (!ContractLiteralCodec.IsDefined(method))
        {
            throw new ArgumentOutOfRangeException(nameof(method), method, "Unity IPC method must be defined.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        IpcResponse? sessionTokenRejection = null;
        Exception? responseInterruption = null;
        var requestId = Guid.NewGuid();
        var acquisitionScope = sessionAcquisitionCoordinator.CreateScope(deadline);
        var sessionAcquisition = await acquisitionScope.ResolveCurrentAsync(
                unityProject,
                cancellationToken)
            .ConfigureAwait(false);

        while (true)
        {
            switch (sessionAcquisition.Kind)
            {
                case DaemonSessionAcquisitionKind.Success:
                    break;
                case DaemonSessionAcquisitionKind.RequestDeadlineExpired:
                    return responseInterruption is null
                        ? CreateDeadlineExceededResult(timeout)
                        : CreateResponseInterruptionResult(responseInterruption, timeout);
                case DaemonSessionAcquisitionKind.PublicationWindowExpired:
                case DaemonSessionAcquisitionKind.SessionNotAvailable:
                    return sessionTokenRejection is not null
                        ? DaemonIpcSendResult.Success(sessionTokenRejection)
                        : DaemonIpcSendResult.Failure(CreateDaemonSessionNotAvailableError());
                case DaemonSessionAcquisitionKind.EndpointAvailabilityWindowExpired:
                    return responseInterruption is null
                        ? DaemonIpcSendResult.Failure(CreateDaemonSessionNotAvailableError())
                        : CreateResponseInterruptionResult(responseInterruption, timeout);
                case DaemonSessionAcquisitionKind.SessionReadFailure:
                    return DaemonIpcSendResult.Failure(ExecutionError.InternalError(
                        $"Daemon session could not be read. {sessionAcquisition.ReadFailure!.Error!.Message}"));
                default:
                    throw new InvalidOperationException(
                        $"Unsupported daemon session acquisition outcome: {sessionAcquisition.Kind}.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
            {
                return CreateDeadlineExceededResult(timeout);
            }

            var session = sessionAcquisition.Session!;
            try
            {
                if (!deadline.TryGetRemainingMilliseconds(out var requestDeadlineRemainingMilliseconds))
                {
                    return CreateDeadlineExceededResult(timeout);
                }

                var request = UnityIpcRequestFactory.Create(
                    session.SessionToken,
                    method,
                    payload,
                    requestId,
                    IpcResponseMode.Single,
                    deadline.UtcDeadline,
                    requestDeadlineRemainingMilliseconds);
                var response = await transportClient.SendAsync(
                        session.Endpoint,
                        request,
                        remainingTimeout,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (IsSessionTokenInvalid(response))
                {
                    sessionTokenRejection = response;
                    responseInterruption = null;
                    sessionAcquisition = await acquisitionScope.ResolveReplacementAsync(
                            unityProject,
                            session,
                            cancellationToken)
                        .ConfigureAwait(false);
                    continue;
                }

                return DaemonIpcSendResult.Success(response);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (reachabilityClassifier.IsRetryableBeforeRequestWrite(exception))
            {
                responseInterruption = null;
                sessionAcquisition = await acquisitionScope.ResolveAfterPreWriteFailureAsync(
                        unityProject,
                        session,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (
                UnityIpcMethodCapabilities.SupportsStatelessReadReplay(method)
                && reachabilityClassifier.IsRecoverableResponseInterruption(exception)
                && !deadline.IsExpired)
            {
                responseInterruption = exception;
                sessionAcquisition = await acquisitionScope.ResolveAfterStatelessResponseInterruptionAsync(
                        unityProject,
                        session,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                return CreateDeadlineExceededResult(timeout);
            }
            catch (Exception exception) when (reachabilityClassifier.IsNotRunning(exception))
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

    private static DaemonIpcSendResult CreateDeadlineExceededResult (TimeSpan timeout)
    {
        return DaemonIpcSendResult.Failure(ExecutionError.Timeout(
            $"Daemon IPC request timed out after {timeout.TotalMilliseconds:0} milliseconds."));
    }

    private static DaemonIpcSendResult CreateResponseInterruptionResult (
        Exception exception,
        TimeSpan timeout)
    {
        return exception is TimeoutException
            ? DaemonIpcSendResult.Failure(ExecutionError.Timeout(
                $"Daemon IPC request timed out after {timeout.TotalMilliseconds:0} milliseconds. {exception.Message}"))
            : DaemonIpcSendResult.Failure(ExecutionError.InternalError(
                $"Daemon IPC request failed. {exception.Message}"));
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

}
