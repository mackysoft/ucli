using System.Net.Sockets;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Reachability;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;

/// <summary> Implements cleanup-specific daemon reachability probing for one project fingerprint. </summary>
internal sealed class DaemonCleanupReachabilityProbe : IDaemonCleanupReachabilityProbe
{
    private readonly IDaemonPingClient daemonPingClient;

    private readonly IDaemonReachabilityClassifier reachabilityClassifier;

    /// <summary> Initializes a new instance of the <see cref="DaemonCleanupReachabilityProbe" /> class. </summary>
    /// <param name="daemonPingClient"> The daemon ping-client dependency. </param>
    /// <param name="reachabilityClassifier"> The daemon reachability-classifier dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonCleanupReachabilityProbe (
        IDaemonPingClient daemonPingClient,
        IDaemonReachabilityClassifier reachabilityClassifier)
    {
        this.daemonPingClient = daemonPingClient ?? throw new ArgumentNullException(nameof(daemonPingClient));
        this.reachabilityClassifier = reachabilityClassifier ?? throw new ArgumentNullException(nameof(reachabilityClassifier));
    }

    /// <summary> Probes daemon reachability using cleanup-specific safety semantics. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="deadline"> The shared cleanup execution deadline. </param>
    /// <param name="sessionToken"> The probe session token to send. Must be non-empty. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns>
    /// <para> One cleanup-specific reachability probe result. </para>
    /// <para> <see cref="DaemonCleanupReachabilityStatus.NotRunning" /> is returned only for direct endpoint-level absence evidence that is strong enough for destructive cleanup. </para>
    /// <para> Ambiguous transport outcomes return <see cref="DaemonCleanupReachabilityStatus.Uncertain" /> even when the recorded daemon process may already be gone. </para>
    /// </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> or <paramref name="sessionToken" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="sessionToken" /> is empty or whitespace. </exception>
    public async ValueTask<DaemonCleanupReachabilityProbeResult> ProbeAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        string sessionToken,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(sessionToken);
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            throw new ArgumentException("Cleanup reachability probe session token must be non-empty.", nameof(sessionToken));
        }

        if (!deadline.TryGetRemainingTimeout(out var pingTimeout))
        {
            return DaemonCleanupReachabilityProbeResult.Failure(ExecutionError.Timeout(
                "Timed out before daemon cleanup reachability probe could begin."));
        }

        // NOTE:
        // This probe must stay conservative because its NotRunning result authorizes destructive
        // cleanup. Only direct endpoint-level absence evidence may map to NotRunning here.
        try
        {
            await daemonPingClient.PingAsync(
                    unityProject,
                    pingTimeout,
                    sessionToken,
                    cancellationToken)
                .ConfigureAwait(false);
            return DaemonCleanupReachabilityProbeResult.Running();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (IpcConnectTimeoutException)
        {
            return DaemonCleanupReachabilityProbeResult.Uncertain(DaemonCleanupReachabilityUncertainReason.ConnectTimeout);
        }
        catch (TimeoutException)
        {
            return DaemonCleanupReachabilityProbeResult.Uncertain(DaemonCleanupReachabilityUncertainReason.Timeout);
        }
        catch (DaemonPingResponseException exception) when (
            exception.ErrorCode == IpcSessionErrorCodes.SessionTokenInvalid
            || exception.ErrorCode == IpcSessionErrorCodes.SessionTokenRequired)
        {
            return DaemonCleanupReachabilityProbeResult.Uncertain(DaemonCleanupReachabilityUncertainReason.SessionAuthenticationRejected);
        }
        catch (SocketException exception) when (DaemonEndpointAbsenceClassifier.IsDirectEndpointAbsence(exception))
        {
            return DaemonCleanupReachabilityProbeResult.NotRunning();
        }
        catch (SocketException)
        {
            return DaemonCleanupReachabilityProbeResult.Uncertain(DaemonCleanupReachabilityUncertainReason.TransportError);
        }
        catch (Exception exception) when (reachabilityClassifier.IsNotRunning(exception))
        {
            return DaemonCleanupReachabilityProbeResult.NotRunning();
        }
        catch (Exception exception)
        {
            return DaemonCleanupReachabilityProbeResult.Failure(ExecutionError.InternalError(
                $"Failed to probe daemon cleanup reachability. {exception.Message}"));
        }
    }

}
