using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Features.Daemon.UseCases.Common;
using MackySoft.Ucli.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Features.Daemon.UseCases.Stop;
using MackySoft.Ucli.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Hosting.Cli;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Shared.Foundation;
using MackySoft.Ucli.UnityIntegration.Ipc;
using MackySoft.Ucli.UnityIntegration.Project;

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
    public async ValueTask<SupervisorReachabilityProbeStatus> ProbeReachability (
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
            var response = await Send(manifest, request, timeout, cancellationToken).ConfigureAwait(false);
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

        if (!IpcTransportKindCodec.TryParse(manifest.EndpointTransportKind, out var transportKind))
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
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The mapped daemon-start result. </returns>
    public async ValueTask<DaemonStartResult> EnsureRunning (
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
                SupervisorIpcContracts.EnsureRunningMethod,
                new SupervisorIpcContracts.EnsureRunningRequest(
                    UnityProjectRoot: unityProject.UnityProjectRoot,
                    ProjectFingerprint: unityProject.ProjectFingerprint,
                    TimeoutMilliseconds: checked((int)timeout.TotalMilliseconds)));
            var response = await Send(manifest, request, timeout, cancellationToken).ConfigureAwait(false);
            if (IpcResponseFailureReader.TryRead(response, out var firstError, out var status))
            {
                return DaemonStartResult.Failure(MapResponseFailure(firstError, status));
            }

            if (!IpcPayloadCodec.TryDeserialize(
                    response.Payload,
                    out SupervisorIpcContracts.EnsureRunningResponse payload,
                    out var payloadError))
            {
                return DaemonStartResult.Failure(ExecutionError.InternalError(
                    $"Supervisor ensureRunning response payload is invalid. {payloadError.Message}"));
            }

            if (string.Equals(payload.StartStatus, DaemonStartStateCodec.Started, StringComparison.Ordinal))
            {
                return DaemonStartResult.Started(payload.Session);
            }

            if (string.Equals(payload.StartStatus, DaemonStartStateCodec.AlreadyRunning, StringComparison.Ordinal))
            {
                return DaemonStartResult.AlreadyRunning(payload.Session);
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
    public async ValueTask<DaemonStopResult> StopProject (
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
            var response = await Send(manifest, request, timeout, cancellationToken).ConfigureAwait(false);
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

            if (string.Equals(payload.StopStatus, DaemonStopStateCodec.Stopped, StringComparison.Ordinal))
            {
                return DaemonStopResult.Stopped();
            }

            if (string.Equals(payload.StopStatus, DaemonStopStateCodec.NotRunning, StringComparison.Ordinal))
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

    private async ValueTask<IpcResponse> Send (
        SupervisorInstanceManifest manifest,
        IpcRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var endpoint = ResolveEndpoint(manifest);
        return await transportClient.SendAsync(endpoint, request, timeout, cancellationToken).ConfigureAwait(false);
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
        if (!IpcTransportKindCodec.TryParse(manifest.EndpointTransportKind, out var transportKind))
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

        if (string.Equals(firstError.Code, IpcErrorCodes.InvalidArgument, StringComparison.Ordinal))
        {
            return ExecutionError.InvalidArgument(firstError.Message);
        }

        if (string.Equals(firstError.Code, CliErrorCodes.IpcTimeout, StringComparison.Ordinal))
        {
            return ExecutionError.Timeout(firstError.Message);
        }

        return ExecutionError.InternalError(firstError.Message);
    }
}