using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Recovery;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.ExistingSession;

/// <summary> Implements existing-session probe flow for daemon start orchestration. </summary>
internal sealed class DaemonExistingSessionGateService : IDaemonExistingSessionGateService
{
    private readonly IDaemonPingInfoClient daemonPingInfoClient;

    private readonly IDaemonReachabilityClassifier reachabilityClassifier;

    private readonly IDaemonSessionCleanupService daemonSessionCleanupService;

    private readonly IDaemonLifecycleStore daemonLifecycleStore;

    private readonly IDaemonProcessIdentityAssessor processIdentityAssessor;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="DaemonExistingSessionGateService" /> class. </summary>
    /// <param name="daemonPingInfoClient"> The daemon ping-info client dependency. </param>
    /// <param name="reachabilityClassifier"> The daemon reachability-classifier dependency. </param>
    /// <param name="daemonSessionCleanupService"> The daemon session-cleanup service dependency. </param>
    /// <param name="daemonLifecycleStore"> The daemon lifecycle observation store dependency. </param>
    /// <param name="processIdentityAssessor"> The daemon process identity assessor dependency. </param>
    /// <param name="timeProvider"> The time provider used for timeout-budget accounting. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonExistingSessionGateService (
        IDaemonPingInfoClient daemonPingInfoClient,
        IDaemonReachabilityClassifier reachabilityClassifier,
        IDaemonSessionCleanupService daemonSessionCleanupService,
        IDaemonLifecycleStore daemonLifecycleStore,
        IDaemonProcessIdentityAssessor processIdentityAssessor,
        TimeProvider? timeProvider = null)
    {
        this.daemonPingInfoClient = daemonPingInfoClient ?? throw new ArgumentNullException(nameof(daemonPingInfoClient));
        this.reachabilityClassifier = reachabilityClassifier ?? throw new ArgumentNullException(nameof(reachabilityClassifier));
        this.daemonSessionCleanupService = daemonSessionCleanupService ?? throw new ArgumentNullException(nameof(daemonSessionCleanupService));
        this.daemonLifecycleStore = daemonLifecycleStore ?? throw new ArgumentNullException(nameof(daemonLifecycleStore));
        this.processIdentityAssessor = processIdentityAssessor ?? throw new ArgumentNullException(nameof(processIdentityAssessor));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Tries to complete daemon start from an existing session.
    /// Returns <see langword="null" /> when caller should continue with fresh launch flow.
    /// </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="session"> The existing daemon session snapshot. </param>
    /// <param name="timeout"> The timeout used for daemon ping and stale cleanup. </param>
    /// <param name="editorMode"> The optional requested daemon Editor mode. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns>
    /// The resolved daemon start result when workflow should complete;
    /// otherwise <see langword="null" /> when fresh launch should continue.
    /// </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> or <paramref name="session" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    public async ValueTask<DaemonStartResult?> TryHandleExistingSessionAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        TimeSpan timeout,
        DaemonEditorMode? editorMode,
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
            var pingResponse = await daemonPingInfoClient.PingAndReadAsync(
                    unityProject,
                    pingTimeout,
                    session.SessionToken,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (!DaemonStartLifecycleSnapshot.TryCreate(pingResponse, out var lifecycleSnapshot, out var lifecycleError))
            {
                return DaemonStartResult.Failure(lifecycleError!);
            }

            if (editorMode.HasValue)
            {
                var requestedEditorMode = DaemonEditorModeCodec.ToValue(editorMode.Value);
                if (!string.Equals(session.EditorMode, requestedEditorMode, StringComparison.Ordinal))
                {
                    return DaemonStartResult.Failure(ExecutionError.InvalidArgument(
                        $"Requested daemon editorMode '{requestedEditorMode}' does not match running daemon editorMode '{session.EditorMode}'.",
                        DaemonErrorCodes.DaemonEditorModeMismatch));
                }
            }

            return DaemonStartResult.AlreadyRunning(session, lifecycleSnapshot);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException exception)
        {
            var recoveringResult = await TryWaitRecoveringSessionAsync(
                    unityProject,
                    session,
                    deadline,
                    editorMode,
                    cancellationToken)
                .ConfigureAwait(false);
            if (recoveringResult is not null)
            {
                return recoveringResult;
            }

            return DaemonStartResult.Failure(ExecutionError.Timeout(
                $"Timed out while probing existing daemon session. {exception.Message}"),
                daemonStatus: DaemonStatusKind.Stale);
        }
        catch (Exception exception) when (reachabilityClassifier.IsNotRunning(exception))
        {
            var recoveringResult = await TryWaitRecoveringSessionAsync(
                    unityProject,
                    session,
                    deadline,
                    editorMode,
                    cancellationToken)
                .ConfigureAwait(false);
            if (recoveringResult is not null)
            {
                return recoveringResult;
            }

            if (!deadline.TryGetRemainingTimeout(out var cleanupTimeout))
            {
                return DaemonStartResult.Failure(ExecutionError.Timeout(
                    "Timed out before stale daemon session cleanup could begin."));
            }

            var cleanupResult = await daemonSessionCleanupService.CleanupStaleSessionArtifactsAsync(
                    unityProject,
                    session,
                    cleanupTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!cleanupResult.IsSuccess)
            {
                return DaemonStartResult.Failure(
                    cleanupResult.Error!,
                    daemonStatus: DaemonStatusKind.Stale);
            }

            return null;
        }
        catch (Exception exception)
        {
            return DaemonStartResult.Failure(ExecutionError.InternalError(
                $"Failed to probe existing daemon session. {exception.Message}"),
                daemonStatus: DaemonStatusKind.Stale);
        }
    }

    private async ValueTask<DaemonStartResult?> TryWaitRecoveringSessionAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        ExecutionDeadline deadline,
        DaemonEditorMode? editorMode,
        CancellationToken cancellationToken)
    {
        var lifecycleReadResult = await daemonLifecycleStore.ReadAsync(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        if (!lifecycleReadResult.IsSuccess
            || !lifecycleReadResult.Exists
            || !lifecycleReadResult.Observation!.IsRecovering
            || !DaemonLifecycleObservationMatcher.MatchesSession(lifecycleReadResult.Observation, session)
            || !IsMatchingLiveProcess(session))
        {
            return null;
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
            {
                return DaemonStartResult.Failure(ExecutionError.Timeout(
                    $"Timed out while waiting for recovering daemon session. ProcessId={session.ProcessId}.",
                    ExecutionErrorCodes.IpcTimeout),
                    daemonStatus: DaemonStatusKind.Stale);
            }

            var attemptTimeout = remainingTimeout < DaemonTimeouts.ProbeAttemptTimeoutCap
                ? remainingTimeout
                : DaemonTimeouts.ProbeAttemptTimeoutCap;
            try
            {
                var pingResponse = await daemonPingInfoClient.PingAndReadAsync(
                        unityProject,
                        attemptTimeout,
                        session.SessionToken,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                if (!DaemonStartLifecycleSnapshot.TryCreate(pingResponse, out var lifecycleSnapshot, out var lifecycleError))
                {
                    return DaemonStartResult.Failure(lifecycleError!);
                }

                if (editorMode.HasValue)
                {
                    var requestedEditorMode = DaemonEditorModeCodec.ToValue(editorMode.Value);
                    if (!string.Equals(session.EditorMode, requestedEditorMode, StringComparison.Ordinal))
                    {
                        return DaemonStartResult.Failure(ExecutionError.InvalidArgument(
                            $"Requested daemon editorMode '{requestedEditorMode}' does not match running daemon editorMode '{session.EditorMode}'.",
                            DaemonErrorCodes.DaemonEditorModeMismatch));
                    }
                }

                return DaemonStartResult.AlreadyRunning(session, lifecycleSnapshot);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (TimeoutException)
            {
            }
            catch (Exception exception) when (reachabilityClassifier.IsNotRunning(exception))
            {
            }
            catch (Exception exception)
            {
                return DaemonStartResult.Failure(ExecutionError.InternalError(
                    $"Failed to probe recovering daemon session. {exception.Message}"),
                    daemonStatus: DaemonStatusKind.Stale);
            }

            if (!deadline.TryGetRemainingTimeout(out remainingTimeout))
            {
                return DaemonStartResult.Failure(ExecutionError.Timeout(
                    $"Timed out while waiting for recovering daemon session. ProcessId={session.ProcessId}.",
                    ExecutionErrorCodes.IpcTimeout),
                    daemonStatus: DaemonStatusKind.Stale);
            }

            await TimeProviderDelay.DelayAsync(GetRetryDelay(remainingTimeout), timeProvider, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private bool IsMatchingLiveProcess (DaemonSession session)
    {
        if (session.ProcessId is not int processId)
        {
            return false;
        }

        return processIdentityAssessor.AssessByProcessId(processId, session.ProcessStartedAtUtc).Status
            == DaemonProcessIdentityAssessmentStatus.MatchingLiveProcess;
    }

    private static TimeSpan GetRetryDelay (TimeSpan remainingTimeout)
    {
        var retryDelayMilliseconds = Math.Min(
            DaemonTimeouts.StartupProbeRetryDelayMilliseconds,
            Math.Max(1, (int)Math.Ceiling(remainingTimeout.TotalMilliseconds)));
        return TimeSpan.FromMilliseconds(retryDelayMilliseconds);
    }
}
