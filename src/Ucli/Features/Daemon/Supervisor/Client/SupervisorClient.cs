using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Contracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.ErrorCodes;
using MackySoft.Ucli.Application.Shared.Execution.Progress;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Features.Daemon.Common.Ipc;
using MackySoft.Ucli.Infrastructure.Execution;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Client;

/// <summary> Sends internal IPC requests to the worktree-local supervisor runtime. </summary>
internal sealed class SupervisorClient
{
    private readonly IIpcTransportClient transportClient;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="SupervisorClient" /> class. </summary>
    /// <param name="transportClient"> The explicit-endpoint transport client dependency. </param>
    /// <param name="timeProvider"> The time source used for bounded response waits. </param>
    public SupervisorClient (
        IIpcTransportClient transportClient,
        TimeProvider timeProvider)
    {
        this.transportClient = transportClient ?? throw new ArgumentNullException(nameof(transportClient));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary> Probes whether the specified supervisor manifest is reachable. </summary>
    /// <param name="manifest"> The supervisor manifest. </param>
    /// <param name="timeout"> The ping timeout. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The classified probe outcome. </returns>
    public async ValueTask<SupervisorReachabilityProbeStatus> ProbeReachabilityAsync (
        SupervisorInstanceManifest manifest,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            if (!deadline.TryGetRemainingTimeout(out var remainingTimeout)
                || !deadline.TryGetRemainingMilliseconds(out var requestDeadlineRemainingMilliseconds))
            {
                return SupervisorReachabilityProbeStatus.TimedOut;
            }

            var request = CreateRequest(
                manifest,
                Guid.NewGuid(),
                SupervisorIpcMethod.Ping,
                new SupervisorIpcContracts.PingRequest(SupervisorConstants.PingClientVersion),
                deadline.UtcDeadline,
                requestDeadlineRemainingMilliseconds,
                IpcResponseMode.Single);

            var sendResult = await ExecutionDeadlineOperation.ExecuteAsync(
                    deadline,
                    cancellationToken,
                    "Timed out before probing supervisor reachability.",
                    "Timed out while probing supervisor reachability.",
                    operationCancellationToken => transportClient.SendAsync(
                        manifest.TransportEndpoint.RuntimeEndpoint,
                        request,
                        remainingTimeout,
                        operationCancellationToken))
                .ConfigureAwait(false);
            if (!sendResult.IsSuccess)
            {
                return SupervisorReachabilityProbeStatus.TimedOut;
            }

            var response = sendResult.Value!;
            if (IpcResponseFailureReader.TryRead(response, out var firstError))
            {
                return firstError?.Code == IpcSessionErrorCodes.SessionTokenInvalid
                    ? SupervisorReachabilityProbeStatus.SessionTokenRejected
                    : SupervisorReachabilityProbeStatus.Unreachable;
            }

            if (!IpcPayloadCodec.TryDeserialize(
                    response.Payload,
                    out SupervisorIpcContracts.PingResponse payload,
                    out _))
            {
                return SupervisorReachabilityProbeStatus.Unreachable;
            }

            return payload.ProcessId == manifest.ProcessId
                && payload.IssuedAtUtc == manifest.IssuedAtUtc
                    ? SupervisorReachabilityProbeStatus.Reachable
                    : SupervisorReachabilityProbeStatus.Unreachable;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (IpcConnectTimeoutException)
        {
            return ShouldTreatConnectTimeoutAsUnreachable(manifest)
                ? SupervisorReachabilityProbeStatus.Unreachable
                : SupervisorReachabilityProbeStatus.TimedOut;
        }
        catch (TimeoutException)
        {
            return SupervisorReachabilityProbeStatus.TimedOut;
        }
        catch (Exception)
        {
            return SupervisorReachabilityProbeStatus.Unreachable;
        }
    }

    private static bool ShouldTreatConnectTimeoutAsUnreachable (SupervisorInstanceManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        if (manifest.Endpoint.TransportKind != IpcTransportKind.NamedPipe)
        {
            return false;
        }

        return manifest.ProcessId > 0 && !ProcessLivenessProbe.IsAlive(manifest.ProcessId);
    }

    /// <summary> Ensures one Unity daemon is running through the supervisor runtime. </summary>
    /// <param name="manifest"> The reachable supervisor manifest. </param>
    /// <param name="requestId"> The stable IPC request identifier for this logical operation. </param>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="deadline"> The shared deadline retained across delivery attempts. </param>
    /// <param name="editorMode"> The optional requested daemon Editor mode. </param>
    /// <param name="onStartupBlocked"> The startup-blocked process policy requested by the caller. </param>
    /// <param name="progressSink"> The optional caller progress sink that receives supervisor-internal daemon-start progress entries. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The mapped daemon-start result. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="requestId" /> is empty. </exception>
    public async ValueTask<DaemonStartResult> EnsureRunningAsync (
        SupervisorInstanceManifest manifest,
        Guid requestId,
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        DaemonEditorMode? editorMode,
        DaemonStartupBlockedProcessPolicy onStartupBlocked,
        ICommandProgressSink? progressSink = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(manifest);
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("Request id must not be empty.", nameof(requestId));
        }

        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(deadline);

        try
        {
            var ensureRunningPayload = new SupervisorIpcContracts.EnsureRunningRequest(
                UnityProjectRoot: unityProject.UnityProjectRoot.Value,
                ProjectFingerprint: unityProject.ProjectFingerprint,
                EditorMode: editorMode,
                OnStartupBlocked: onStartupBlocked);
            if (!deadline.TryGetRemainingTimeout(out var attemptTimeout)
                || !deadline.TryGetRemainingMilliseconds(out var timeoutMilliseconds))
            {
                return DaemonStartResult.Failure(ExecutionError.Timeout(
                    "Timed out before requesting supervisor ensureRunning."));
            }

            var request = CreateRequest(
                manifest,
                requestId,
                SupervisorIpcMethod.EnsureRunning,
                ensureRunningPayload,
                deadline.UtcDeadline,
                timeoutMilliseconds,
                progressSink is null ? IpcResponseMode.Single : IpcResponseMode.Stream);
            var progressFrameForwarder = progressSink is null
                ? null
                : new SupervisorDaemonStartProgressFrameForwarder(
                    progressSink,
                    ensureRunningPayload.ProjectFingerprint,
                    timeoutMilliseconds,
                    editorMode,
                    onStartupBlocked);
            var terminalResponseDeadline = deadline.CreateCompletionDeadline(
                SupervisorConstants.EnsureRunningTerminalResponseGrace);
            var terminalResponseResult = await ExecutionDeadlineOperation.ExecuteAsync(
                    terminalResponseDeadline,
                    cancellationToken,
                    "Timed out before waiting for the supervisor ensureRunning terminal response.",
                    "Timed out while waiting for the supervisor ensureRunning terminal response.",
                    operationCancellationToken => progressFrameForwarder is null
                        ? transportClient.SendWithUnboundedResponseWaitAsync(
                            manifest.TransportEndpoint.RuntimeEndpoint,
                            request,
                            attemptTimeout,
                            operationCancellationToken)
                        : transportClient.SendStreamingWithUnboundedResponseWaitAsync(
                            manifest.TransportEndpoint.RuntimeEndpoint,
                            request,
                            attemptTimeout,
                            progressFrameForwarder.ForwardAsync,
                            operationCancellationToken))
                .ConfigureAwait(false);
            if (!terminalResponseResult.IsSuccess)
            {
                return DaemonStartResult.Failure(terminalResponseResult.Error!);
            }

            var response = terminalResponseResult.Value!;
            if (IpcResponseFailureReader.TryRead(response, out var firstError))
            {
                var failurePayload = TryReadEnsureRunningFailurePayload(response);
                return DaemonStartResult.Failure(
                    MapResponseFailure(firstError),
                    failurePayload?.Diagnosis,
                    failurePayload?.Startup,
                    failurePayload?.DaemonStatus == DaemonStatusKind.Stale
                        ? DaemonStatusKind.Stale
                        : DaemonStatusKind.NotRunning);
            }

            if (!IpcPayloadCodec.TryDeserialize(
                    response.Payload,
                    out SupervisorIpcContracts.EnsureRunningResponse payload,
                    out var payloadError))
            {
                return DaemonStartResult.Failure(ExecutionError.InternalError(
                    $"Supervisor ensureRunning response payload is invalid. {payloadError.Message}"));
            }

            if (payload.Session is null || payload.LifecycleObservation is null)
            {
                return DaemonStartResult.Failure(ExecutionError.InternalError(
                    "Supervisor ensureRunning response is missing its session or lifecycle observation."));
            }

            if (!DaemonSessionIpcTransportEndpointAdapter.TryCreate(
                    payload.Session,
                    unityProject.ProjectFingerprint,
                    "received from the supervisor ensureRunning response.",
                    out var session,
                    out var sessionError))
            {
                return DaemonStartResult.Failure(ExecutionError.InternalError(
                    $"Supervisor ensureRunning response session is invalid. {sessionError.Message}"));
            }

            return payload.StartStatus switch
            {
                DaemonStartStatus.Started => DaemonStartResult.Started(session, payload.LifecycleObservation),
                DaemonStartStatus.AlreadyRunning => DaemonStartResult.AlreadyRunning(session, payload.LifecycleObservation),
                DaemonStartStatus.Attached => DaemonStartResult.Attached(session, payload.LifecycleObservation),
                _ => DaemonStartResult.Failure(ExecutionError.InternalError(
                    $"Supervisor ensureRunning returned unsupported startStatus: {ContractLiteralCodec.ToValue(payload.StartStatus)}.")),
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException exception)
        {
            return DaemonStartResult.Failure(ExecutionError.Timeout(
                $"Timed out while requesting supervisor ensureRunning. {exception.Message}"));
        }
        catch (Exception exception)
        {
            return DaemonStartResult.Failure(ExecutionError.InternalError(
                $"Failed to request supervisor ensureRunning. {exception.Message}"));
        }
    }

    /// <summary> Stops one Unity daemon through the supervisor runtime. </summary>
    /// <param name="manifest"> The reachable supervisor manifest. </param>
    /// <param name="requestId"> The stable IPC request identifier for this logical operation. </param>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="deadline"> The shared deadline retained across delivery attempts. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The mapped daemon-stop result. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="requestId" /> is empty. </exception>
    public async ValueTask<DaemonStopResult> StopProjectAsync (
        SupervisorInstanceManifest manifest,
        Guid requestId,
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(manifest);
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("Request id must not be empty.", nameof(requestId));
        }

        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(deadline);

        try
        {
            if (!deadline.TryGetRemainingTimeout(out var attemptTimeout)
                || !deadline.TryGetRemainingMilliseconds(out var timeoutMilliseconds))
            {
                return DaemonStopResult.Failure(ExecutionError.Timeout(
                    "Timed out before requesting supervisor stopProject."));
            }

            var request = CreateRequest(
                manifest,
                requestId,
                SupervisorIpcMethod.StopProject,
                new SupervisorIpcContracts.StopProjectRequest(
                    UnityProjectRoot: unityProject.UnityProjectRoot.Value,
                    ProjectFingerprint: unityProject.ProjectFingerprint),
                deadline.UtcDeadline,
                timeoutMilliseconds,
                IpcResponseMode.Single);
            var terminalResponseDeadline = deadline.CreateCompletionDeadline(
                SupervisorConstants.StopProjectTerminalResponseGrace);
            var terminalResponseResult = await ExecutionDeadlineOperation.ExecuteAsync(
                    terminalResponseDeadline,
                    cancellationToken,
                    "Timed out before waiting for the supervisor stopProject terminal response.",
                    "Timed out while waiting for the supervisor stopProject terminal response.",
                    operationCancellationToken => transportClient.SendWithUnboundedResponseWaitAsync(
                        manifest.TransportEndpoint.RuntimeEndpoint,
                        request,
                        attemptTimeout,
                        operationCancellationToken))
                .ConfigureAwait(false);
            if (!terminalResponseResult.IsSuccess)
            {
                return DaemonStopResult.Failure(terminalResponseResult.Error!);
            }

            var response = terminalResponseResult.Value!;
            if (IpcResponseFailureReader.TryRead(response, out var firstError))
            {
                return DaemonStopResult.Failure(MapResponseFailure(firstError));
            }

            if (!IpcPayloadCodec.TryDeserialize(
                    response.Payload,
                    out SupervisorIpcContracts.StopProjectResponse payload,
                    out var payloadError))
            {
                return DaemonStopResult.Failure(ExecutionError.InternalError(
                    $"Supervisor stopProject response payload is invalid. {payloadError.Message}"));
            }

            return payload.StopStatus switch
            {
                DaemonStopStatus.Stopped => DaemonStopResult.Stopped(),
                DaemonStopStatus.NotRunning => DaemonStopResult.NotRunning(),
                _ => throw new InvalidOperationException($"Unsupported daemon stop status: {payload.StopStatus}."),
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException exception)
        {
            return DaemonStopResult.Failure(ExecutionError.Timeout(
                $"Timed out while requesting supervisor stopProject. {exception.Message}"));
        }
        catch (Exception exception)
        {
            return DaemonStopResult.Failure(ExecutionError.InternalError(
                $"Failed to request supervisor stopProject. {exception.Message}"));
        }
    }

    private static IpcRequestEnvelope CreateRequest<TPayload> (
        SupervisorInstanceManifest manifest,
        Guid requestId,
        SupervisorIpcMethod method,
        TPayload payload,
        DateTimeOffset requestDeadlineUtc,
        int requestDeadlineRemainingMilliseconds,
        IpcResponseMode responseMode)
    {
        return new IpcRequestEnvelope(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: requestId,
            sessionToken: manifest.SessionToken.GetEncodedValue(),
            method: ContractLiteralCodec.ToValue(method),
            payload: IpcPayloadCodec.SerializeToElement(payload),
            responseMode: ContractLiteralCodec.ToValue(responseMode),
            requestDeadlineUtc: requestDeadlineUtc,
            requestDeadlineRemainingMilliseconds: requestDeadlineRemainingMilliseconds);
    }

    private static ExecutionError MapResponseFailure (IpcError firstError)
    {
        if (firstError.Code == UcliCoreErrorCodes.InvalidArgument)
        {
            return ExecutionError.InvalidArgument(firstError.Message, firstError.Code);
        }

        if (firstError.Code == ExecutionErrorCodes.IpcTimeout)
        {
            return ExecutionError.Timeout(firstError.Message, firstError.Code);
        }

        if (firstError.Code == DaemonErrorCodes.DaemonEditorModeMismatch)
        {
            return ExecutionError.InvalidArgument(firstError.Message, firstError.Code);
        }

        return ExecutionError.InternalError(firstError.Message, firstError.Code);
    }

    private static SupervisorEnsureRunningFailureMetadata? TryReadEnsureRunningFailurePayload (IpcResponse response)
    {
        if (!IpcPayloadCodec.TryDeserialize(
                response.Payload,
                out SupervisorIpcContracts.EnsureRunningFailureResponse payload,
                out _)
            || !SupervisorEnsureRunningFailurePayloadMapper.TryToMetadata(payload, out var metadata))
        {
            return null;
        }

        return metadata;
    }

}
