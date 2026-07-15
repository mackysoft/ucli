using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Progress;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Recovery;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.ExistingSession;

/// <summary> Implements existing-session probe flow for daemon start orchestration. </summary>
internal sealed class DaemonExistingSessionGateService : IDaemonExistingSessionGateService
{
    private readonly IDaemonPingInfoClient daemonPingInfoClient;

    private readonly IDaemonReachabilityClassifier reachabilityClassifier;

    private readonly IDaemonSessionCleanupService daemonSessionCleanupService;

    private readonly IDaemonLifecycleStore daemonLifecycleStore;

    private readonly IDaemonProcessIdentityAssessor processIdentityAssessor;

    private readonly IDaemonSessionStore daemonSessionStore;

    /// <summary> Initializes a new instance of the <see cref="DaemonExistingSessionGateService" /> class. </summary>
    /// <param name="daemonPingInfoClient"> The daemon ping-info client dependency. </param>
    /// <param name="reachabilityClassifier"> The daemon reachability-classifier dependency. </param>
    /// <param name="daemonSessionCleanupService"> The daemon session-cleanup service dependency. </param>
    /// <param name="daemonLifecycleStore"> The daemon lifecycle observation store dependency. </param>
    /// <param name="processIdentityAssessor"> The daemon process identity assessor dependency. </param>
    /// <param name="daemonSessionStore"> The daemon session store used to observe replacement session publication during recovery. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonExistingSessionGateService (
        IDaemonPingInfoClient daemonPingInfoClient,
        IDaemonReachabilityClassifier reachabilityClassifier,
        IDaemonSessionCleanupService daemonSessionCleanupService,
        IDaemonLifecycleStore daemonLifecycleStore,
        IDaemonProcessIdentityAssessor processIdentityAssessor,
        IDaemonSessionStore daemonSessionStore)
    {
        this.daemonPingInfoClient = daemonPingInfoClient ?? throw new ArgumentNullException(nameof(daemonPingInfoClient));
        this.reachabilityClassifier = reachabilityClassifier ?? throw new ArgumentNullException(nameof(reachabilityClassifier));
        this.daemonSessionCleanupService = daemonSessionCleanupService ?? throw new ArgumentNullException(nameof(daemonSessionCleanupService));
        this.daemonLifecycleStore = daemonLifecycleStore ?? throw new ArgumentNullException(nameof(daemonLifecycleStore));
        this.processIdentityAssessor = processIdentityAssessor ?? throw new ArgumentNullException(nameof(processIdentityAssessor));
        this.daemonSessionStore = daemonSessionStore ?? throw new ArgumentNullException(nameof(daemonSessionStore));
    }

    /// <summary>
    /// Tries to complete daemon start from an existing session.
    /// Returns <see langword="null" /> when caller should continue with the remaining start flow.
    /// </summary>
    /// <param name="unityProject"> The resolved Unity project context. </param>
    /// <param name="session"> The existing daemon session snapshot. </param>
    /// <param name="deadline"> The deadline shared by the daemon-start workflow. </param>
    /// <param name="editorMode"> The optional requested daemon Editor mode. </param>
    /// <param name="progressObserver"> The optional observer for supervisor-internal start progress. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns>
    /// The resolved daemon start result when workflow should complete;
    /// otherwise <see langword="null" /> when the remaining start flow should continue.
    /// </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProject" /> or <paramref name="session" /> is <see langword="null" />. </exception>
    public async ValueTask<DaemonStartResult?> TryHandleExistingSessionAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        ExecutionDeadline deadline,
        DaemonEditorMode? editorMode,
        IDaemonStartProgressObserver? progressObserver = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(deadline);

        if (!deadline.TryGetRemainingTimeout(out var pingTimeout))
        {
            return DaemonStartResult.Failure(ExecutionError.Timeout(
                "Timed out before probing existing daemon session could begin."));
        }

        try
        {
            await EmitWaitingForEndpointAsync(progressObserver, session, cancellationToken).ConfigureAwait(false);
            if (!deadline.TryGetRemainingTimeout(out pingTimeout))
            {
                return DaemonStartResult.Failure(ExecutionError.Timeout(
                    "Timed out before probing existing daemon session could begin."));
            }

            // NOTE: A blocked endpoint must leave time for identity-validated GUI rebootstrap.
            // Lifecycle state cannot decide this cap because a stale endpoint may still publish ready.
            var initialPingTimeout = session.EditorMode == DaemonEditorMode.Gui
                ? GetShortestTimeout(pingTimeout, DaemonTimeouts.ProbeAttemptTimeoutCap)
                : pingTimeout;
            var initialPingDeadline = session.EditorMode == DaemonEditorMode.Gui
                && pingTimeout > DaemonTimeouts.ProbeAttemptTimeoutCap
                    ? deadline.CreateCappedDeadline(DaemonTimeouts.ProbeAttemptTimeoutCap)
                    : deadline;
            var pingOperation = await ExecutionDeadlineOperation.ExecuteAsync(
                    initialPingDeadline,
                    cancellationToken,
                    "Timed out before probing existing daemon session could begin.",
                    "Timed out while probing existing daemon session.",
                    token => daemonPingInfoClient.PingSessionAndReadAsync(
                        unityProject,
                        session,
                        initialPingTimeout,
                        validateProjectFingerprint: true,
                        cancellationToken: token))
                .ConfigureAwait(false);
            if (!pingOperation.IsSuccess)
            {
                throw new TimeoutException(pingOperation.Error!.Message);
            }

            var pingResponse = pingOperation.Value!;
            var lifecycleObservation = pingResponse;

            var editorModeMismatchResult = CreateEditorModeMismatchResult(session, editorMode);
            if (editorModeMismatchResult is not null)
            {
                return editorModeMismatchResult;
            }

            await EmitEndpointReadyAsync(progressObserver, session, lifecycleObservation, cancellationToken).ConfigureAwait(false);
            return DaemonStartResult.AlreadyRunning(session, lifecycleObservation);
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
                    progressObserver,
                    cancellationToken)
                .ConfigureAwait(false);
            if (recoveringResult.Disposition == RecoveringSessionGateDisposition.Complete)
            {
                return recoveringResult.Result!;
            }

            if (recoveringResult.Disposition == RecoveringSessionGateDisposition.ContinueStartFlow)
            {
                return null;
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
                    progressObserver,
                    cancellationToken)
                .ConfigureAwait(false);
            if (recoveringResult.Disposition == RecoveringSessionGateDisposition.Complete)
            {
                return recoveringResult.Result!;
            }

            if (recoveringResult.Disposition == RecoveringSessionGateDisposition.ContinueStartFlow)
            {
                return null;
            }

            if (!deadline.TryGetRemainingTimeout(out _))
            {
                return DaemonStartResult.Failure(ExecutionError.Timeout(
                    "Timed out before stale daemon session cleanup could begin."));
            }

            var cleanupResult = await daemonSessionCleanupService.CleanupStaleSessionArtifactsAsync(
                    unityProject,
                    session,
                    deadline,
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

    private async ValueTask<RecoveringSessionGateResult> TryWaitRecoveringSessionAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        ExecutionDeadline deadline,
        DaemonEditorMode? editorMode,
        IDaemonStartProgressObserver? progressObserver,
        CancellationToken cancellationToken)
    {
        var lifecycleObservation = await ReadUsableGuiLifecycleObservationAsync(
                unityProject,
                session,
                deadline.Clock,
                cancellationToken)
            .ConfigureAwait(false);
        if (lifecycleObservation is null)
        {
            return RecoveringSessionGateResult.NotRecovering();
        }

        var editorModeMismatchResult = CreateEditorModeMismatchResult(session, editorMode);
        if (editorModeMismatchResult is not null)
        {
            return RecoveringSessionGateResult.Complete(editorModeMismatchResult);
        }

        // A fresh sidecar for the same live GUI Editor proves that destructive stale cleanup is unsafe.
        // When the endpoint is not in a transient recovery state, hand off immediately so the GUI
        // supervisor can replace or quarantine the unresponsive endpoint.
        if (!lifecycleObservation.IsRecovering)
        {
            return RecoveringSessionGateResult.ContinueStartFlow();
        }

        if (!deadline.TryGetRemainingTimeout(out var remainingWorkflowTimeout))
        {
            return RecoveringSessionGateResult.Complete(DaemonStartResult.Failure(ExecutionError.Timeout(
                $"Timed out while waiting for recovering daemon session. ProcessId={session.ProcessId}.",
                ExecutionErrorCodes.IpcTimeout),
                daemonStatus: DaemonStatusKind.Stale));
        }

        // NOTE:
        // A recovering sidecar proves that the endpoint may return, but it must not consume
        // the whole start timeout. After one probe window, the outer start workflow can use
        // GUI attach/rebootstrap to replace the stale registration when the process is alive.
        var recoveryDeadline = deadline.CreateCappedDeadline(
            GetRecoveringSessionProbeTimeout(remainingWorkflowTimeout));
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!deadline.TryGetRemainingTimeout(out var remainingTimeout))
            {
                return RecoveringSessionGateResult.Complete(DaemonStartResult.Failure(ExecutionError.Timeout(
                    $"Timed out while waiting for recovering daemon session. ProcessId={session.ProcessId}.",
                    ExecutionErrorCodes.IpcTimeout),
                    daemonStatus: DaemonStatusKind.Stale));
            }

            if (!recoveryDeadline.TryGetRemainingTimeout(out var remainingRecoveryTimeout))
            {
                return RecoveringSessionGateResult.ContinueStartFlow();
            }

            var attemptTimeout = GetShortestTimeout(
                remainingTimeout,
                remainingRecoveryTimeout,
                DaemonTimeouts.ProbeAttemptTimeoutCap);
            ExecutionDeadline attemptDeadline;
            if (remainingTimeout <= remainingRecoveryTimeout
                && remainingTimeout <= DaemonTimeouts.ProbeAttemptTimeoutCap)
            {
                attemptDeadline = deadline;
            }
            else if (remainingRecoveryTimeout <= DaemonTimeouts.ProbeAttemptTimeoutCap)
            {
                attemptDeadline = recoveryDeadline;
            }
            else
            {
                attemptDeadline = recoveryDeadline.CreateCappedDeadline(DaemonTimeouts.ProbeAttemptTimeoutCap);
            }

            var sessionReadResult = await daemonSessionStore.ReadAsync(
                    unityProject.RepositoryRoot,
                    unityProject.ProjectFingerprint,
                    cancellationToken)
                .ConfigureAwait(false);
            if (sessionReadResult.IsSuccess && sessionReadResult.Exists)
            {
                var publishedSession = sessionReadResult.Session!;
                if (!DaemonLifecycleObservationMatcher.MatchesSessionByEditorInstance(
                        lifecycleObservation,
                        publishedSession))
                {
                    return RecoveringSessionGateResult.ContinueStartFlow();
                }

                session = publishedSession;
            }

            try
            {
                var pingOperation = await ExecutionDeadlineOperation.ExecuteAsync(
                        attemptDeadline,
                        cancellationToken,
                        $"Timed out before probing recovering daemon session. ProcessId={session.ProcessId}.",
                        $"Timed out while probing recovering daemon session. ProcessId={session.ProcessId}.",
                        token => daemonPingInfoClient.PingSessionAndReadAsync(
                            unityProject,
                            session,
                            attemptTimeout,
                            validateProjectFingerprint: true,
                            cancellationToken: token))
                    .ConfigureAwait(false);
                if (pingOperation.IsSuccess)
                {
                    var pingObservation = pingOperation.Value!;

                    editorModeMismatchResult = CreateEditorModeMismatchResult(session, editorMode);
                    if (editorModeMismatchResult is not null)
                    {
                        return RecoveringSessionGateResult.Complete(editorModeMismatchResult);
                    }

                    await EmitEndpointReadyAsync(progressObserver, session, pingObservation, cancellationToken).ConfigureAwait(false);
                    return RecoveringSessionGateResult.Complete(DaemonStartResult.AlreadyRunning(session, pingObservation));
                }
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
                return RecoveringSessionGateResult.Complete(DaemonStartResult.Failure(ExecutionError.InternalError(
                    $"Failed to probe recovering daemon session. {exception.Message}"),
                    daemonStatus: DaemonStatusKind.Stale));
            }

            if (!deadline.TryGetRemainingTimeout(out remainingTimeout))
            {
                return RecoveringSessionGateResult.Complete(DaemonStartResult.Failure(ExecutionError.Timeout(
                    $"Timed out while waiting for recovering daemon session. ProcessId={session.ProcessId}.",
                    ExecutionErrorCodes.IpcTimeout),
                    daemonStatus: DaemonStatusKind.Stale));
            }

            if (!recoveryDeadline.TryGetRemainingTimeout(out remainingRecoveryTimeout))
            {
                return RecoveringSessionGateResult.ContinueStartFlow();
            }

            await TimeProviderDelay.DelayAsync(
                    GetRetryDelay(GetShortestTimeout(remainingTimeout, remainingRecoveryTimeout)),
                    deadline.Clock,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async ValueTask<DaemonLifecycleObservation?> ReadUsableGuiLifecycleObservationAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (session.EditorMode != DaemonEditorMode.Gui)
        {
            return null;
        }

        var lifecycleReadResult = await daemonLifecycleStore.ReadAsync(
                unityProject.RepositoryRoot,
                unityProject.ProjectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        var observation = lifecycleReadResult.Observation;
        if (!lifecycleReadResult.IsSuccess
            || !lifecycleReadResult.Exists
            || observation is null
            || !DaemonLifecycleObservationAvailability.IsUsableForSession(
                observation,
                session,
                processIdentityAssessor,
                timeProvider))
        {
            return null;
        }

        return observation;
    }

    private static DaemonStartResult? CreateEditorModeMismatchResult (
        DaemonSession session,
        DaemonEditorMode? editorMode)
    {
        if (!editorMode.HasValue)
        {
            return null;
        }

        if (session.EditorMode == editorMode.Value)
        {
            return null;
        }

        var requestedEditorMode = ContractLiteralCodec.ToValue(editorMode.Value);
        var runningEditorMode = ContractLiteralCodec.ToValue(session.EditorMode);
        return DaemonStartResult.Failure(ExecutionError.InvalidArgument(
            $"Requested daemon editorMode '{requestedEditorMode}' does not match running daemon editorMode '{runningEditorMode}'.",
            DaemonErrorCodes.DaemonEditorModeMismatch));
    }

    private static TimeSpan GetRetryDelay (TimeSpan remainingTimeout)
    {
        var retryDelayMilliseconds = Math.Min(
            DaemonTimeouts.StartupProbeRetryDelayMilliseconds,
            Math.Max(1, (int)Math.Ceiling(remainingTimeout.TotalMilliseconds)));
        return TimeSpan.FromMilliseconds(retryDelayMilliseconds);
    }

    private static TimeSpan GetRecoveringSessionProbeTimeout (TimeSpan remainingTimeout)
    {
        return remainingTimeout < DaemonTimeouts.ProbeAttemptTimeoutCap
            ? remainingTimeout
            : DaemonTimeouts.ProbeAttemptTimeoutCap;
    }

    private static TimeSpan GetShortestTimeout (
        TimeSpan first,
        TimeSpan second)
    {
        return first < second ? first : second;
    }

    private static TimeSpan GetShortestTimeout (
        TimeSpan first,
        TimeSpan second,
        TimeSpan third)
    {
        return GetShortestTimeout(GetShortestTimeout(first, second), third);
    }

    private static async ValueTask EmitWaitingForEndpointAsync (
        IDaemonStartProgressObserver? progressObserver,
        DaemonSession session,
        CancellationToken cancellationToken)
    {
        if (progressObserver is null)
        {
            return;
        }

        await progressObserver.EmitWaitingForEndpointAsync(
                new DaemonStartStartupProgressObservation(
                    LaunchAttemptId: null,
                    EditorMode: session.EditorMode,
                    OwnerKind: session.OwnerKind,
                    CanShutdownProcess: session.CanShutdownProcess,
                    ProcessId: session.ProcessId,
                    ProcessStartedAtUtc: session.ProcessStartedAtUtc,
                    StartupStatus: null,
                    StartupBlockingReason: null,
                    StartupPhase: null,
                    RetryDisposition: null,
                    Message: null,
                    ErrorCode: null),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask EmitEndpointReadyAsync (
        IDaemonStartProgressObserver? progressObserver,
        DaemonSession session,
        IpcUnityEditorObservation lifecycleObservation,
        CancellationToken cancellationToken)
    {
        if (progressObserver is null)
        {
            return;
        }

        await progressObserver.EmitEndpointRegisteredAsync(session, launchAttemptId: null, cancellationToken).ConfigureAwait(false);
        await progressObserver.EmitLifecycleObservedAsync(lifecycleObservation, cancellationToken).ConfigureAwait(false);
    }

    private enum RecoveringSessionGateDisposition
    {
        NotRecovering,
        Complete,
        ContinueStartFlow,
    }

    private readonly record struct RecoveringSessionGateResult (
        RecoveringSessionGateDisposition Disposition,
        DaemonStartResult? Result)
    {
        public static RecoveringSessionGateResult NotRecovering ()
        {
            return new RecoveringSessionGateResult(RecoveringSessionGateDisposition.NotRecovering, null);
        }

        public static RecoveringSessionGateResult Complete (DaemonStartResult result)
        {
            ArgumentNullException.ThrowIfNull(result);

            return new RecoveringSessionGateResult(RecoveringSessionGateDisposition.Complete, result);
        }

        public static RecoveringSessionGateResult ContinueStartFlow ()
        {
            return new RecoveringSessionGateResult(RecoveringSessionGateDisposition.ContinueStartFlow, null);
        }
    }
}
