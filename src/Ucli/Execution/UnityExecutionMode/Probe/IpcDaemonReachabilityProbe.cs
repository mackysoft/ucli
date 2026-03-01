using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Ipc;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Execution;

/// <summary> Probes daemon reachability by attempting an IPC ping call. </summary>
internal sealed class IpcDaemonReachabilityProbe : IDaemonReachabilityProbe
{
    private readonly IIpcEndpointResolver endpointResolver;

    private readonly IDaemonPingClient daemonPingClient;

    /// <summary> Initializes a new instance of the <see cref="IpcDaemonReachabilityProbe" /> class. </summary>
    /// <param name="endpointResolver"> The endpoint resolver dependency. </param>
    /// <param name="daemonPingClient"> The daemon ping client dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="endpointResolver" /> or <paramref name="daemonPingClient" /> is <see langword="null" />. </exception>
    public IpcDaemonReachabilityProbe (
        IIpcEndpointResolver endpointResolver,
        IDaemonPingClient daemonPingClient)
    {
        this.endpointResolver = endpointResolver ?? throw new ArgumentNullException(nameof(endpointResolver));
        this.daemonPingClient = daemonPingClient ?? throw new ArgumentNullException(nameof(daemonPingClient));
    }

    /// <summary> Probes whether daemon for the specified project is reachable. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The probe timeout. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon reachability probe result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    public async ValueTask<DaemonReachabilityProbeResult> Probe (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();

        var endpoint = endpointResolver.Resolve(
            unityProject.RepositoryRoot,
            unityProject.ProjectFingerprint);

        // NOTE:
        // For Unix domain sockets, skip network probing when the socket file is absent.
        // This avoids waiting for connection timeout when daemon is clearly not running.
        if (endpoint.TransportKind == IpcTransportKind.UnixDomainSocket
            && !File.Exists(endpoint.Address))
        {
            return DaemonReachabilityProbeResult.NotRunning();
        }

        try
        {
            await daemonPingClient.Ping(
                    unityProject,
                    timeout,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return DaemonReachabilityProbeResult.Running();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (DaemonProbeExceptionClassifier.IsNotRunning(exception))
        {
            return DaemonReachabilityProbeResult.NotRunning();
        }
        catch (Exception exception)
        {
            return DaemonReachabilityProbeResult.Failure(
                ExecutionError.InternalError($"Failed to probe daemon reachability. {exception.Message}"));
        }
    }
}
