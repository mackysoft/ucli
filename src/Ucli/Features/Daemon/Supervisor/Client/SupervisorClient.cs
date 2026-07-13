using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
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
        try
        {
            var request = CreateRequest(
                manifest,
                Guid.NewGuid(),
                SupervisorIpcMethod.Ping,
                new SupervisorIpcContracts.PingRequest(SupervisorConstants.PingClientVersion));
            var response = await SendAsync(manifest, request, timeout, cancellationToken).ConfigureAwait(false);
            if (IpcResponseFailureReader.TryRead(response, out var firstError, out _))
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
    /// <param name="deadlineUtc"> The shared absolute command deadline. </param>
    /// <param name="attemptTimeout"> The monotonic budget remaining when this delivery attempt begins. </param>
    /// <param name="editorMode"> The optional requested daemon Editor mode. </param>
    /// <param name="onStartupBlocked"> The startup-blocked process policy requested by the caller. </param>
    /// <param name="progressSink"> The optional caller progress sink that receives supervisor-internal daemon-start progress entries. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The mapped daemon-start result. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="requestId" /> is empty. </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="attemptTimeout" /> cannot be represented by the IPC millisecond contract.
    /// </exception>
    public async ValueTask<DaemonStartResult> EnsureRunningAsync (
        SupervisorInstanceManifest manifest,
        Guid requestId,
        ResolvedUnityProjectContext unityProject,
        DateTimeOffset deadlineUtc,
        TimeSpan attemptTimeout,
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
        var timeoutMilliseconds = ValidateAttemptTimeout(attemptTimeout, "ensureRunning");
        var terminalResponseTimeout = attemptTimeout.Add(SupervisorConstants.EnsureRunningTerminalResponseGrace);

        try
        {
            var ensureRunningPayload = new SupervisorIpcContracts.EnsureRunningRequest(
                    UnityProjectRoot: unityProject.UnityProjectRoot,
                    ProjectFingerprint: unityProject.ProjectFingerprint,
                    DeadlineUtc: deadlineUtc,
                    AttemptTimeoutMilliseconds: timeoutMilliseconds,
                    EditorMode: editorMode.HasValue
                    ? ContractLiteralCodec.ToValue(editorMode.Value)
                    : null,
                OnStartupBlocked: ContractLiteralCodec.ToValue(onStartupBlocked));
            var request = CreateRequest(
                manifest,
                requestId,
                SupervisorIpcMethod.EnsureRunning,
                ensureRunningPayload,
                progressSink is null ? IpcResponseMode.Single : IpcResponseMode.Stream);
            var progressFrameForwarder = progressSink is null
                ? null
                : new SupervisorDaemonStartProgressFrameForwarder(
                    progressSink,
                    ensureRunningPayload.ProjectFingerprint,
                    timeoutMilliseconds,
                    editorMode,
                    onStartupBlocked);
            var terminalResponseDeadline = ExecutionDeadline.Start(
                terminalResponseTimeout,
                timeProvider);
            var terminalResponseResult = await ExecutionDeadlineOperation.ExecuteAsync(
                    terminalResponseDeadline,
                    cancellationToken,
                    "Timed out before waiting for the supervisor ensureRunning terminal response.",
                    "Timed out while waiting for the supervisor ensureRunning terminal response.",
                    operationCancellationToken => progressFrameForwarder is null
                        ? transportClient.SendWithUnboundedResponseWaitAsync(
                            manifest.Endpoint,
                            request,
                            attemptTimeout,
                            operationCancellationToken)
                        : transportClient.SendStreamingWithUnboundedResponseWaitAsync(
                            manifest.Endpoint,
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
            if (IpcResponseFailureReader.TryRead(response, out var firstError, out var status))
            {
                var failurePayload = TryReadEnsureRunningFailurePayload(response);
                return DaemonStartResult.Failure(
                    MapResponseFailure(firstError, status),
                    failurePayload?.Diagnosis,
                    failurePayload?.Startup,
                    ResolveDaemonStatus(failurePayload?.DaemonStatus));
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

            if (!DaemonSessionContractMapper.TryCreate(
                    payload.Session,
                    unityProject.ProjectFingerprint,
                    "received from the supervisor ensureRunning response.",
                    out var session,
                    out var sessionError))
            {
                return DaemonStartResult.Failure(ExecutionError.InternalError(
                    $"Supervisor ensureRunning response session is invalid. {sessionError.Message}"));
            }

            if (ContractLiteralCodec.Matches(payload.StartStatus, DaemonStartStatus.Started))
            {
                return DaemonStartResult.Started(session, payload.LifecycleObservation);
            }

            if (ContractLiteralCodec.Matches(payload.StartStatus, DaemonStartStatus.AlreadyRunning))
            {
                return DaemonStartResult.AlreadyRunning(session, payload.LifecycleObservation);
            }

            if (ContractLiteralCodec.Matches(payload.StartStatus, DaemonStartStatus.Attached))
            {
                return DaemonStartResult.Attached(session, payload.LifecycleObservation);
            }

            return DaemonStartResult.Failure(ExecutionError.InternalError(
                $"Supervisor ensureRunning returned unsupported startStatus: {payload.StartStatus}."));
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
    /// <param name="deadlineUtc"> The shared absolute command deadline. </param>
    /// <param name="attemptTimeout"> The monotonic budget remaining when this delivery attempt begins. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The mapped daemon-stop result. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="requestId" /> is empty. </exception>
    public async ValueTask<DaemonStopResult> StopProjectAsync (
        SupervisorInstanceManifest manifest,
        Guid requestId,
        ResolvedUnityProjectContext unityProject,
        DateTimeOffset deadlineUtc,
        TimeSpan attemptTimeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(manifest);
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("Request id must not be empty.", nameof(requestId));
        }

        ArgumentNullException.ThrowIfNull(unityProject);
        var timeoutMilliseconds = ValidateAttemptTimeout(attemptTimeout, "stopProject");

        try
        {
            var request = CreateRequest(
                manifest,
                requestId,
                SupervisorIpcMethod.StopProject,
                new SupervisorIpcContracts.StopProjectRequest(
                    UnityProjectRoot: unityProject.UnityProjectRoot,
                    ProjectFingerprint: unityProject.ProjectFingerprint,
                    DeadlineUtc: deadlineUtc,
                    AttemptTimeoutMilliseconds: timeoutMilliseconds));
            var terminalResponseDeadline = ExecutionDeadline.Start(
                attemptTimeout.Add(SupervisorConstants.StopProjectTerminalResponseGrace),
                timeProvider);
            var terminalResponseResult = await ExecutionDeadlineOperation.ExecuteAsync(
                    terminalResponseDeadline,
                    cancellationToken,
                    "Timed out before waiting for the supervisor stopProject terminal response.",
                    "Timed out while waiting for the supervisor stopProject terminal response.",
                    operationCancellationToken => transportClient.SendWithUnboundedResponseWaitAsync(
                        manifest.Endpoint,
                        request,
                        attemptTimeout,
                        operationCancellationToken))
                .ConfigureAwait(false);
            if (!terminalResponseResult.IsSuccess)
            {
                return DaemonStopResult.Failure(terminalResponseResult.Error!);
            }

            var response = terminalResponseResult.Value!;
            if (IpcResponseFailureReader.TryRead(response, out var firstError, out var status))
            {
                return DaemonStopResult.Failure(MapResponseFailure(firstError, status));
            }

            if (!IpcPayloadCodec.TryDeserialize(
                    response.Payload,
                    out SupervisorIpcContracts.StopProjectResponse payload,
                    out var payloadError))
            {
                return DaemonStopResult.Failure(ExecutionError.InternalError(
                    $"Supervisor stopProject response payload is invalid. {payloadError.Message}"));
            }

            if (ContractLiteralCodec.Matches(payload.StopStatus, DaemonStopStatus.Stopped))
            {
                return DaemonStopResult.Stopped();
            }

            if (ContractLiteralCodec.Matches(payload.StopStatus, DaemonStopStatus.NotRunning))
            {
                return DaemonStopResult.NotRunning();
            }

            return DaemonStopResult.Failure(ExecutionError.InternalError(
                $"Supervisor stopProject returned unsupported stopStatus: {payload.StopStatus}."));
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

    private async ValueTask<IpcResponse> SendAsync (
        SupervisorInstanceManifest manifest,
        IpcRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        return await transportClient.SendAsync(manifest.Endpoint, request, timeout, cancellationToken).ConfigureAwait(false);
    }

    private static int ValidateAttemptTimeout (
        TimeSpan attemptTimeout,
        string operation)
    {
        if (attemptTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(attemptTimeout),
                attemptTimeout,
                $"Supervisor {operation} attempt timeout must be greater than zero.");
        }

        if (attemptTimeout.TotalMilliseconds > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(attemptTimeout),
                attemptTimeout,
                $"Supervisor {operation} attempt timeout must not exceed {int.MaxValue} milliseconds.");
        }

        return checked((int)Math.Ceiling(attemptTimeout.TotalMilliseconds));
    }

    private static IpcRequest CreateRequest<TPayload> (
        SupervisorInstanceManifest manifest,
        Guid requestId,
        SupervisorIpcMethod method,
        TPayload payload,
        IpcResponseMode responseMode = IpcResponseMode.Single)
    {
        if (!ContractLiteralCodec.TryToValue(method, out var methodLiteral))
        {
            throw new ArgumentOutOfRangeException(nameof(method), method, "Supervisor IPC method must be specified.");
        }

        return new IpcRequest(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: requestId,
            sessionToken: manifest.SessionToken.GetEncodedValue(),
            method: methodLiteral,
            payload: IpcPayloadCodec.SerializeToElement(payload),
            responseMode: ContractLiteralCodec.ToValue(responseMode));
    }

    private static ExecutionError MapResponseFailure (
        IpcError? firstError,
        string? status)
    {
        if (firstError == null)
        {
            return ExecutionError.InternalError(
                $"Supervisor response failed with unexpected status: {status ?? "<null>"}.");
        }

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

    private static SupervisorIpcContracts.EnsureRunningFailureResponse? TryReadEnsureRunningFailurePayload (IpcResponse response)
    {
        return IpcPayloadCodec.TryDeserialize(
            response.Payload,
            out SupervisorIpcContracts.EnsureRunningFailureResponse payload,
            out _)
            ? payload
            : null;
    }

    private static DaemonStatusKind ResolveDaemonStatus (string? daemonStatus)
    {
        return ContractLiteralCodec.Matches(daemonStatus, DaemonStatusKind.Stale)
            ? DaemonStatusKind.Stale
            : DaemonStatusKind.NotRunning;
    }
}
