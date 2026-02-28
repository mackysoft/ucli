using System.Net.Sockets;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Ipc;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Execution;

/// <summary> Probes daemon reachability by attempting an IPC ping call. </summary>
internal sealed class IpcDaemonReachabilityProbe : IDaemonReachabilityProbe
{
    private const string ProbeSessionToken = "mode-probe";

    private const string ProbeClientVersion = "ucli-mode-probe";

    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromMilliseconds(300);

    private readonly IIpcEndpointResolver endpointResolver;

    private readonly IUnityIpcClient unityIpcClient;

    /// <summary> Initializes a new instance of the <see cref="IpcDaemonReachabilityProbe" /> class. </summary>
    /// <param name="endpointResolver"> The endpoint resolver dependency. </param>
    /// <param name="unityIpcClient"> The Unity IPC client dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="endpointResolver" /> or <paramref name="unityIpcClient" /> is <see langword="null" />. </exception>
    public IpcDaemonReachabilityProbe (
        IIpcEndpointResolver endpointResolver,
        IUnityIpcClient unityIpcClient)
    {
        this.endpointResolver = endpointResolver ?? throw new ArgumentNullException(nameof(endpointResolver));
        this.unityIpcClient = unityIpcClient ?? throw new ArgumentNullException(nameof(unityIpcClient));
    }

    /// <summary> Probes whether daemon for the specified project is reachable. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon reachability probe result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    public async ValueTask<DaemonReachabilityProbeResult> ProbeAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        cancellationToken.ThrowIfCancellationRequested();

        var endpoint = endpointResolver.Resolve(
            unityProject.UnityProjectRoot,
            unityProject.ProjectFingerprint);

        // NOTE:
        // For Unix domain sockets, skip network probing when the socket file is absent.
        // This avoids waiting for connection timeout when daemon is clearly not running.
        if (endpoint.TransportKind == IpcTransportKind.UnixDomainSocket
            && !File.Exists(endpoint.Address))
        {
            return DaemonReachabilityProbeResult.NotRunning();
        }

        var pingRequest = CreatePingRequest();
        using var timeoutCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellationTokenSource.CancelAfter(ProbeTimeout);
        try
        {
            await unityIpcClient.SendAsync(
                    unityProject.UnityProjectRoot,
                    unityProject.ProjectFingerprint,
                    pingRequest,
                    timeoutCancellationTokenSource.Token)
                .ConfigureAwait(false);
            return DaemonReachabilityProbeResult.Running();
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested && timeoutCancellationTokenSource.IsCancellationRequested)
        {
            return DaemonReachabilityProbeResult.NotRunning();
        }
        catch (TimeoutException)
        {
            return DaemonReachabilityProbeResult.NotRunning();
        }
        catch (Exception exception) when (IsConnectivityFailure(exception))
        {
            return DaemonReachabilityProbeResult.NotRunning();
        }
        catch (Exception exception)
        {
            return DaemonReachabilityProbeResult.Failure(
                ExecutionError.InternalError($"Failed to probe daemon reachability. {exception.Message}"));
        }
    }

    /// <summary> Creates one IPC ping request used for reachability probing. </summary>
    /// <returns> The ping request envelope. </returns>
    private static IpcRequest CreatePingRequest ()
    {
        var payload = JsonSerializer.SerializeToElement(
            new IpcPingRequest(ProbeClientVersion),
            IpcJsonSerializerOptions.Default);
        return new IpcRequest(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: $"mode-probe-{Guid.NewGuid():N}",
            SessionToken: ProbeSessionToken,
            Method: IpcMethodNames.Ping,
            Payload: payload);
    }

    /// <summary> Determines whether an exception indicates endpoint reachability failure. </summary>
    /// <param name="exception"> The exception to classify. </param>
    /// <returns> <see langword="true" /> when the exception means endpoint is not reachable; otherwise <see langword="false" />. </returns>
    private static bool IsConnectivityFailure (Exception exception)
    {
        return exception is IOException
            or SocketException
            or UnauthorizedAccessException;
    }
}
