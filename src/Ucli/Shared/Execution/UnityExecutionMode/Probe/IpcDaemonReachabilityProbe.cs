using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Timing;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Recovery;

namespace MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;

/// <summary> Probes daemon reachability by attempting an IPC ping call. </summary>
internal sealed class IpcDaemonReachabilityProbe : IDaemonReachabilityProbe
{
    private readonly IDaemonPingClient daemonPingClient;

    private readonly UnityDaemonRecoveryWaiter? recoveryWaiter;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="IpcDaemonReachabilityProbe" /> class. </summary>
    /// <param name="daemonPingClient"> The daemon ping client dependency. </param>
    /// <param name="recoveryWaiter"> The daemon lifecycle recovery waiter dependency. </param>
    /// <param name="timeProvider"> The time provider used for timeout-budget accounting. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="daemonPingClient" /> is <see langword="null" />. </exception>
    public IpcDaemonReachabilityProbe (
        IDaemonPingClient daemonPingClient,
        UnityDaemonRecoveryWaiter? recoveryWaiter,
        TimeProvider timeProvider)
    {
        this.daemonPingClient = daemonPingClient ?? throw new ArgumentNullException(nameof(daemonPingClient));
        this.recoveryWaiter = recoveryWaiter;
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary> Probes whether daemon for the specified project is reachable. </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="timeout"> The probe timeout. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The daemon reachability probe result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    public async ValueTask<DaemonReachabilityProbeResult> ProbeAsync (
        ResolvedUnityProjectContext unityProject,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();
        var deadline = ExecutionDeadline.Start(timeout, timeProvider);

        var endpoint = UcliIpcEndpointResolver.ResolveDaemonEndpoint(
            unityProject.RepositoryRoot,
            unityProject.ProjectFingerprint);

        // NOTE:
        // For Unix domain sockets, skip network probing when the socket file is absent.
        // This avoids waiting for connection timeout when daemon is clearly not running.
        if (endpoint.TransportKind == IpcTransportKind.UnixDomainSocket
            && !File.Exists(endpoint.Address))
        {
            if (recoveryWaiter == null)
            {
                return DaemonReachabilityProbeResult.NotRunning();
            }

            var recoveryDelayConsumed = await recoveryWaiter
                .DelayIfRecoveringAsync(unityProject, deadline, cancellationToken)
                .ConfigureAwait(false);
            if (!deadline.TryGetRemainingTimeout(out _))
            {
                return DaemonReachabilityProbeResult.Failure(ExecutionError.Timeout(
                    $"Timed out while probing daemon reachability. Timeout={timeout.TotalMilliseconds:0}ms."));
            }

            if (!recoveryDelayConsumed)
            {
                return DaemonReachabilityProbeResult.NotRunning();
            }
        }

        if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
        {
            return DaemonReachabilityProbeResult.Failure(ExecutionError.Timeout(
                $"Timed out while probing daemon reachability. Timeout={timeout.TotalMilliseconds:0}ms."));
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!deadline.TryGetRemainingTimeout(out remainingTimeout))
            {
                return DaemonReachabilityProbeResult.Failure(ExecutionError.Timeout(
                    $"Timed out while probing daemon reachability. Timeout={timeout.TotalMilliseconds:0}ms."));
            }

            var attemptTimeout = remainingTimeout < DaemonTimeouts.ProbeAttemptTimeoutCap
                ? remainingTimeout
                : DaemonTimeouts.ProbeAttemptTimeoutCap;
            try
            {
                await daemonPingClient.PingAsync(
                        unityProject,
                        attemptTimeout,
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
                var recoveryDelayConsumed = recoveryWaiter != null
                    && await recoveryWaiter.DelayIfRecoveringAsync(unityProject, deadline, cancellationToken).ConfigureAwait(false);
                if (!deadline.TryGetRemainingTimeout(out remainingTimeout))
                {
                    return DaemonReachabilityProbeResult.Failure(ExecutionError.Timeout(
                        $"Timed out while probing daemon reachability. Timeout={timeout.TotalMilliseconds:0}ms."));
                }

                if (recoveryDelayConsumed)
                {
                    continue;
                }

                return DaemonReachabilityProbeResult.NotRunning();
            }
            catch (TimeoutException)
            {
                if (!deadline.TryGetRemainingTimeout(out remainingTimeout))
                {
                    return DaemonReachabilityProbeResult.Failure(ExecutionError.Timeout(
                        $"Timed out while probing daemon reachability. Timeout={timeout.TotalMilliseconds:0}ms."));
                }

                await TimeProviderDelay.DelayAsync(GetRetryDelay(remainingTimeout), timeProvider, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                return DaemonReachabilityProbeResult.Failure(
                    ExecutionError.InternalError($"Failed to probe daemon reachability. {exception.Message}"));
            }
        }
    }

    private static TimeSpan GetRetryDelay (TimeSpan remainingTimeout)
    {
        var retryDelayMilliseconds = Math.Min(
            DaemonTimeouts.StartupProbeRetryDelayMilliseconds,
            Math.Max(1, (int)Math.Ceiling(remainingTimeout.TotalMilliseconds)));
        return TimeSpan.FromMilliseconds(retryDelayMilliseconds);
    }
}
