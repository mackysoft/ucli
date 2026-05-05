using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Application.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Stop;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Process;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;

/// <summary> Implements existing-session probe flow for daemon start orchestration. </summary>
internal sealed class DaemonExistingSessionGateService : IDaemonExistingSessionGateService
{
    private readonly IDaemonPingClient daemonPingClient;

    private readonly IDaemonReachabilityClassifier reachabilityClassifier;

    private readonly IDaemonSessionCleanupService daemonSessionCleanupService;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="DaemonExistingSessionGateService" /> class. </summary>
    /// <param name="daemonPingClient"> The daemon ping-client dependency. </param>
    /// <param name="reachabilityClassifier"> The daemon reachability-classifier dependency. </param>
    /// <param name="daemonSessionCleanupService"> The daemon session-cleanup service dependency. </param>
    /// <param name="timeProvider"> The time provider used for timeout-budget accounting. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonExistingSessionGateService (
        IDaemonPingClient daemonPingClient,
        IDaemonReachabilityClassifier reachabilityClassifier,
        IDaemonSessionCleanupService daemonSessionCleanupService,
        TimeProvider? timeProvider = null)
    {
        this.daemonPingClient = daemonPingClient ?? throw new ArgumentNullException(nameof(daemonPingClient));
        this.reachabilityClassifier = reachabilityClassifier ?? throw new ArgumentNullException(nameof(reachabilityClassifier));
        this.daemonSessionCleanupService = daemonSessionCleanupService ?? throw new ArgumentNullException(nameof(daemonSessionCleanupService));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Tries to complete daemon start from an existing session.
    /// Returns <see langword="null" /> when caller should continue with fresh launch flow.
    /// </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="session"> The existing daemon session snapshot. </param>
    /// <param name="timeout"> The timeout used for daemon ping and stale cleanup. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns>
    /// The resolved daemon start result when workflow should complete;
    /// otherwise <see langword="null" /> when fresh launch should continue.
    /// </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> or <paramref name="session" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    public async ValueTask<DaemonStartResult?> TryHandleExistingSession (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        var deadline = ExecutionDeadline.Start(timeout, timeProvider);

        if (!deadline.TryGetRemainingTimeout(out var pingTimeout))
        {
            return DaemonStartResult.Failure(ExecutionError.Timeout(
                "Timed out before probing existing daemon session could begin."));
        }

        try
        {
            await daemonPingClient.Ping(
                    unityProject,
                    pingTimeout,
                    session.SessionToken,
                    cancellationToken)
                .ConfigureAwait(false);
            return DaemonStartResult.AlreadyRunning(session);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException exception)
        {
            return DaemonStartResult.Failure(ExecutionError.Timeout(
                $"Timed out while probing existing daemon session. {exception.Message}"));
        }
        catch (Exception exception) when (reachabilityClassifier.IsNotRunning(exception))
        {
            if (!deadline.TryGetRemainingTimeout(out var cleanupTimeout))
            {
                return DaemonStartResult.Failure(ExecutionError.Timeout(
                    "Timed out before stale daemon session cleanup could begin."));
            }

            var cleanupResult = await daemonSessionCleanupService.CleanupStaleSessionArtifacts(
                    unityProject,
                    session,
                    cleanupTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!cleanupResult.IsSuccess)
            {
                return DaemonStartResult.Failure(cleanupResult.Error!);
            }

            return null;
        }
        catch (Exception exception)
        {
            return DaemonStartResult.Failure(ExecutionError.InternalError(
                $"Failed to probe existing daemon session. {exception.Message}"));
        }
    }
}
