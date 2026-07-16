using System.Net.Sockets;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;

/// <summary> Implements cleanup-specific daemon reachability probing for one project fingerprint. </summary>
internal sealed class DaemonCleanupReachabilityProbe : IDaemonCleanupReachabilityProbe
{
    private readonly IDaemonPingClient daemonPingClient;

    /// <summary> Initializes a new instance of the <see cref="DaemonCleanupReachabilityProbe" /> class. </summary>
    /// <param name="daemonPingClient"> The daemon ping-client dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="daemonPingClient" /> is <see langword="null" />. </exception>
    public DaemonCleanupReachabilityProbe (IDaemonPingClient daemonPingClient)
    {
        this.daemonPingClient = daemonPingClient ?? throw new ArgumentNullException(nameof(daemonPingClient));
    }

    /// <summary> Probes daemon reachability without presenting a session token. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="deadline"> The shared cleanup execution deadline. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns>
    /// <para> One cleanup-specific reachability probe result. </para>
    /// <para> <see cref="DaemonCleanupReachabilityStatus.NotRunning" /> is returned only for direct endpoint-level absence evidence that is strong enough for destructive cleanup. </para>
    /// <para> Ambiguous transport outcomes return <see cref="DaemonCleanupReachabilityStatus.Uncertain" /> even when the recorded daemon process may already be gone. </para>
    /// </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    public ValueTask<DaemonCleanupReachabilityProbeResult> ProbeWithoutSessionTokenAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(deadline);

        return ProbeCoreAsync(
            unityProject,
            deadline,
            sessionToken: null,
            cancellationToken);
    }

    /// <summary> Probes daemon reachability using a known session token. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="deadline"> The shared cleanup execution deadline. </param>
    /// <param name="sessionToken"> The validated session token to present. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> One cleanup-specific reachability probe result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> or <paramref name="sessionToken" /> is <see langword="null" />. </exception>
    public ValueTask<DaemonCleanupReachabilityProbeResult> ProbeWithSessionTokenAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        IpcSessionToken sessionToken,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(deadline);
        ArgumentNullException.ThrowIfNull(sessionToken);

        return ProbeCoreAsync(
            unityProject,
            deadline,
            sessionToken,
            cancellationToken);
    }

    private async ValueTask<DaemonCleanupReachabilityProbeResult> ProbeCoreAsync (
        ResolvedUnityProjectContext unityProject,
        ExecutionDeadline deadline,
        IpcSessionToken? sessionToken,
        CancellationToken cancellationToken)
    {
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
            if (sessionToken is null)
            {
                await daemonPingClient.PingCanonicalEndpointWithoutSessionTokenAsync(
                        unityProject,
                        pingTimeout,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                await daemonPingClient.PingCanonicalEndpointWithSessionTokenAsync(
                        unityProject,
                        pingTimeout,
                        sessionToken,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

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
            DaemonProbeExceptionClassifier.IsSessionAuthenticationRejected(exception.ErrorCode))
        {
            return DaemonCleanupReachabilityProbeResult.Uncertain(DaemonCleanupReachabilityUncertainReason.SessionAuthenticationRejected);
        }
        catch (IpcConnectException exception) when (DaemonEndpointAbsenceClassifier.IsDirectEndpointAbsence(exception))
        {
            return DaemonCleanupReachabilityProbeResult.NotRunning();
        }
        catch (IpcConnectException)
        {
            return DaemonCleanupReachabilityProbeResult.Uncertain(DaemonCleanupReachabilityUncertainReason.TransportError);
        }
        catch (SocketException)
        {
            return DaemonCleanupReachabilityProbeResult.Uncertain(DaemonCleanupReachabilityUncertainReason.TransportError);
        }
        catch (Exception exception)
        {
            return DaemonCleanupReachabilityProbeResult.Failure(ExecutionError.InternalError(
                $"Failed to probe daemon cleanup reachability. {exception.Message}"));
        }
    }

}
