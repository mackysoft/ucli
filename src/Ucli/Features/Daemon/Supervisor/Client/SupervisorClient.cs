using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Contracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.ErrorCodes;
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

    /// <summary> Initializes a new instance of the <see cref="SupervisorClient" /> class. </summary>
    /// <param name="transportClient"> The explicit-endpoint transport client dependency. </param>
    public SupervisorClient (IIpcTransportClient transportClient)
    {
        this.transportClient = transportClient ?? throw new ArgumentNullException(nameof(transportClient));
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
                SupervisorIpcContracts.PingMethod,
                new SupervisorIpcContracts.PingRequest(SupervisorConstants.PingClientVersion));
            var response = await SendAsync(manifest, request, timeout, cancellationToken).ConfigureAwait(false);
            if (IpcResponseFailureReader.TryRead(response, out _, out _))
            {
                return SupervisorReachabilityProbeStatus.Unreachable;
            }

            return IpcPayloadCodec.TryDeserialize(response.Payload, out SupervisorIpcContracts.PingResponse _, out _)
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

        if (!ContractLiteralCodec.TryParse<IpcTransportKind>(manifest.EndpointTransportKind, out var transportKind))
        {
            return true;
        }

        if (transportKind != IpcTransportKind.NamedPipe)
        {
            return false;
        }

        return manifest.ProcessId > 0 && !ProcessLivenessProbe.IsAlive(manifest.ProcessId);
    }

    /// <summary> Ensures one Unity daemon is running through the supervisor runtime. </summary>
    /// <param name="manifest"> The reachable supervisor manifest. </param>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The command timeout. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="editorMode"> The optional requested daemon Editor mode. </param>
    /// <param name="onStartupBlocked"> The startup-blocked process policy requested by the caller. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The mapped daemon-start result. </returns>
    public async ValueTask<DaemonStartResult> EnsureRunningAsync (
        SupervisorInstanceManifest manifest,
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        DaemonEditorMode? editorMode,
        DaemonStartupBlockedProcessPolicy onStartupBlocked,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        try
        {
            var request = CreateRequest(
                manifest,
                SupervisorIpcContracts.EnsureRunningMethod,
                new SupervisorIpcContracts.EnsureRunningRequest(
                    UnityProjectRoot: unityProject.UnityProjectRoot,
                    ProjectFingerprint: unityProject.ProjectFingerprint,
                    TimeoutMilliseconds: checked((int)timeout.TotalMilliseconds),
                    EditorMode: editorMode.HasValue
                        ? ContractLiteralCodec.ToValue(editorMode.Value)
                        : null,
                    OnStartupBlocked: ContractLiteralCodec.ToValue(onStartupBlocked)));
            var response = await SendWithUnboundedResponseWaitAsync(manifest, request, timeout, cancellationToken).ConfigureAwait(false);
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

            if (string.Equals(payload.StartStatus, ContractLiteralCodec.ToValue(DaemonStartStatus.Started), StringComparison.Ordinal))
            {
                return DaemonStartResult.Started(payload.Session, payload.LifecycleSnapshot);
            }

            if (string.Equals(payload.StartStatus, ContractLiteralCodec.ToValue(DaemonStartStatus.AlreadyRunning), StringComparison.Ordinal))
            {
                return DaemonStartResult.AlreadyRunning(payload.Session, payload.LifecycleSnapshot);
            }

            if (string.Equals(payload.StartStatus, ContractLiteralCodec.ToValue(DaemonStartStatus.Attached), StringComparison.Ordinal))
            {
                return DaemonStartResult.Attached(payload.Session, payload.LifecycleSnapshot);
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
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The command timeout. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The mapped daemon-stop result. </returns>
    public async ValueTask<DaemonStopResult> StopProjectAsync (
        SupervisorInstanceManifest manifest,
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        try
        {
            var request = CreateRequest(
                manifest,
                SupervisorIpcContracts.StopProjectMethod,
                new SupervisorIpcContracts.StopProjectRequest(
                    UnityProjectRoot: unityProject.UnityProjectRoot,
                    ProjectFingerprint: unityProject.ProjectFingerprint,
                    TimeoutMilliseconds: checked((int)timeout.TotalMilliseconds)));
            var response = await SendAsync(manifest, request, timeout, cancellationToken).ConfigureAwait(false);
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

            if (string.Equals(payload.StopStatus, ContractLiteralCodec.ToValue(DaemonStopStatus.Stopped), StringComparison.Ordinal))
            {
                return DaemonStopResult.Stopped();
            }

            if (string.Equals(payload.StopStatus, ContractLiteralCodec.ToValue(DaemonStopStatus.NotRunning), StringComparison.Ordinal))
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
        var endpoint = ResolveEndpoint(manifest);
        return await transportClient.SendAsync(endpoint, request, timeout, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<IpcResponse> SendWithUnboundedResponseWaitAsync (
        SupervisorInstanceManifest manifest,
        IpcRequest request,
        TimeSpan sendTimeout,
        CancellationToken cancellationToken)
    {
        var endpoint = ResolveEndpoint(manifest);
        return await transportClient.SendWithUnboundedResponseWaitAsync(endpoint, request, sendTimeout, cancellationToken).ConfigureAwait(false);
    }

    private static IpcRequest CreateRequest<TPayload> (
        SupervisorInstanceManifest manifest,
        string method,
        TPayload payload)
    {
        return new IpcRequest(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: $"supervisor-{Guid.NewGuid():N}",
            SessionToken: manifest.SessionToken,
            Method: method,
            Payload: IpcPayloadCodec.SerializeToElement(payload));
    }

    private static IpcEndpoint ResolveEndpoint (SupervisorInstanceManifest manifest)
    {
        if (!ContractLiteralCodec.TryParse<IpcTransportKind>(manifest.EndpointTransportKind, out var transportKind))
        {
            throw new InvalidOperationException(
                $"Supervisor manifest endpointTransportKind is invalid: {manifest.EndpointTransportKind}.");
        }

        return new IpcEndpoint(transportKind, manifest.EndpointAddress);
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
        return string.Equals(daemonStatus, ContractLiteralCodec.ToValue(DaemonStatusKind.Stale), StringComparison.Ordinal)
            ? DaemonStatusKind.Stale
            : DaemonStatusKind.NotRunning;
    }
}
