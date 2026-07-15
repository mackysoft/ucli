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
    private readonly DaemonSessionProbe daemonSessionProbe;

    private readonly IDaemonReachabilityClassifier reachabilityClassifier;

    private readonly IDaemonSessionCleanupService daemonSessionCleanupService;

    private readonly IDaemonLifecycleStore daemonLifecycleStore;

    private readonly IDaemonProcessIdentityAssessor processIdentityAssessor;

    /// <summary> Initializes a new instance of the <see cref="DaemonExistingSessionGateService" /> class. </summary>
    /// <param name="daemonSessionProbe"> The exact-session reachability probe dependency. </param>
    /// <param name="reachabilityClassifier"> The daemon reachability-classifier dependency. </param>
    /// <param name="daemonSessionCleanupService"> The daemon session-cleanup service dependency. </param>
    /// <param name="daemonLifecycleStore"> The daemon lifecycle observation store dependency. </param>
    /// <param name="processIdentityAssessor"> The daemon process identity assessor dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public DaemonExistingSessionGateService (
        DaemonSessionProbe daemonSessionProbe,
        IDaemonReachabilityClassifier reachabilityClassifier,
        IDaemonSessionCleanupService daemonSessionCleanupService,
        IDaemonLifecycleStore daemonLifecycleStore,
        IDaemonProcessIdentityAssessor processIdentityAssessor)
    {
        this.daemonSessionProbe = daemonSessionProbe ?? throw new ArgumentNullException(nameof(daemonSessionProbe));
        this.reachabilityClassifier = reachabilityClassifier ?? throw new ArgumentNullException(nameof(reachabilityClassifier));
        this.daemonSessionCleanupService = daemonSessionCleanupService ?? throw new ArgumentNullException(nameof(daemonSessionCleanupService));
        this.daemonLifecycleStore = daemonLifecycleStore ?? throw new ArgumentNullException(nameof(daemonLifecycleStore));
        this.processIdentityAssessor = processIdentityAssessor ?? throw new ArgumentNullException(nameof(processIdentityAssessor));
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

        await EmitWaitingForEndpointAsync(progressObserver, session, cancellationToken).ConfigureAwait(false);
        if (!deadline.TryGetRemainingTimeout(out pingTimeout))
        {
            return DaemonStartResult.Failure(ExecutionError.Timeout(
                "Timed out before probing existing daemon session could begin."));
        }

        // A blocked endpoint must leave time for identity-validated GUI rebootstrap.
        // Lifecycle state cannot decide this cap because a stale endpoint may still publish ready.
        var initialProbeDeadline = session.EditorMode == DaemonEditorMode.Gui
            && pingTimeout > DaemonTimeouts.ProbeAttemptTimeoutCap
                ? deadline.CreateCappedDeadline(DaemonTimeouts.ProbeAttemptTimeoutCap)
                : deadline;
        var probeResult = await daemonSessionProbe.ProbeAsync(
                unityProject,
                session,
                initialProbeDeadline,
                cancellationToken)
            .ConfigureAwait(false);
        if (probeResult.IsSuccess)
        {
            if (probeResult.Session.SessionGenerationId != session.SessionGenerationId
                && !MatchesExpectedSessionIdentity(session, probeResult.Session, unityProject))
            {
                return null;
            }

            var editorModeMismatchResult = CreateEditorModeMismatchResult(probeResult.Session, editorMode);
            if (editorModeMismatchResult is not null)
            {
                return editorModeMismatchResult;
            }

            await EmitEndpointReadyAsync(
                    progressObserver,
                    probeResult.Session,
                    probeResult.PingResponse,
                    cancellationToken)
                .ConfigureAwait(false);
            return DaemonStartResult.AlreadyRunning(probeResult.Session, probeResult.PingResponse);
        }

        if (probeResult.SessionReadFailure is not null)
        {
            return DaemonStartResult.Failure(ExecutionError.InternalError(
                $"Failed to read a replacement daemon session. {probeResult.SessionReadFailure.Error!.Message}"),
                daemonStatus: DaemonStatusKind.Stale);
        }

        var probeFailure = probeResult.ProbeFailure!;
        var isTimeout = probeFailure is TimeoutException;
        var isNotRunning = reachabilityClassifier.IsNotRunning(probeFailure);
        if (!isTimeout && !isNotRunning)
        {
            return DaemonStartResult.Failure(ExecutionError.InternalError(
                $"Failed to probe existing daemon session. {probeFailure.Message}"),
                daemonStatus: DaemonStatusKind.Stale);
        }

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

        if (isTimeout)
        {
            return DaemonStartResult.Failure(ExecutionError.Timeout(
                $"Timed out while probing existing daemon session. {probeFailure.Message}"),
                daemonStatus: DaemonStatusKind.Stale);
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
        return cleanupResult.IsSuccess
            ? null
            : DaemonStartResult.Failure(
                cleanupResult.Error!,
                daemonStatus: DaemonStatusKind.Stale);
    }

    private async ValueTask<RecoveringSessionGateResult> TryWaitRecoveringSessionAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        ExecutionDeadline deadline,
        DaemonEditorMode? editorMode,
        IDaemonStartProgressObserver? progressObserver,
        CancellationToken cancellationToken)
    {
        var lifecycleReadOperation = await ReadUsableGuiLifecycleObservationAsync(
                unityProject,
                session,
                deadline,
                cancellationToken)
            .ConfigureAwait(false);
        if (!lifecycleReadOperation.IsSuccess)
        {
            return RecoveringSessionGateResult.Complete(DaemonStartResult.Failure(
                lifecycleReadOperation.Error!,
                daemonStatus: DaemonStatusKind.Stale));
        }

        var lifecycleObservation = lifecycleReadOperation.Value;
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

        if (!deadline.TryGetRemainingTimeout(out _))
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
        var recoveryDeadline = deadline.CreateCappedDeadline(DaemonTimeouts.ProbeAttemptTimeoutCap);
        var probeResult = await daemonSessionProbe.ProbeAsync(
                unityProject,
                session,
                recoveryDeadline,
                cancellationToken)
            .ConfigureAwait(false);
        if (probeResult.IsSuccess)
        {
            if (!MatchesExpectedSessionIdentity(session, probeResult.Session, unityProject)
                || !DaemonLifecycleObservationMatcher.MatchesSessionByEditorInstance(
                    lifecycleObservation,
                    probeResult.Session))
            {
                return RecoveringSessionGateResult.ContinueStartFlow();
            }

            editorModeMismatchResult = CreateEditorModeMismatchResult(probeResult.Session, editorMode);
            if (editorModeMismatchResult is not null)
            {
                return RecoveringSessionGateResult.Complete(editorModeMismatchResult);
            }

            await EmitEndpointReadyAsync(
                    progressObserver,
                    probeResult.Session,
                    probeResult.PingResponse,
                    cancellationToken)
                .ConfigureAwait(false);
            return RecoveringSessionGateResult.Complete(DaemonStartResult.AlreadyRunning(
                probeResult.Session,
                probeResult.PingResponse));
        }

        if (probeResult.SessionReadFailure is not null)
        {
            return RecoveringSessionGateResult.Complete(DaemonStartResult.Failure(ExecutionError.InternalError(
                $"Failed to read a recovering daemon session. {probeResult.SessionReadFailure.Error!.Message}"),
                daemonStatus: DaemonStatusKind.Stale));
        }

        if (!deadline.TryGetRemainingTimeout(out _))
        {
            return RecoveringSessionGateResult.Complete(DaemonStartResult.Failure(ExecutionError.Timeout(
                $"Timed out while waiting for recovering daemon session. ProcessId={session.ProcessId}.",
                ExecutionErrorCodes.IpcTimeout),
                daemonStatus: DaemonStatusKind.Stale));
        }

        var probeFailure = probeResult.ProbeFailure!;
        return probeFailure is TimeoutException
            || reachabilityClassifier.IsNotRunning(probeFailure)
            || reachabilityClassifier.IsSessionTokenInvalid(probeFailure)
            || reachabilityClassifier.IsRecoverableResponseInterruption(probeFailure)
                ? RecoveringSessionGateResult.ContinueStartFlow()
                : RecoveringSessionGateResult.Complete(DaemonStartResult.Failure(ExecutionError.InternalError(
                    $"Failed to probe recovering daemon session. {probeFailure.Message}"),
                    daemonStatus: DaemonStatusKind.Stale));
    }

    private async ValueTask<ExecutionDeadlineOperationResult<DaemonLifecycleObservation?>> ReadUsableGuiLifecycleObservationAsync (
        ResolvedUnityProjectContext unityProject,
        DaemonSession session,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken)
    {
        if (session.EditorMode != DaemonEditorMode.Gui)
        {
            return ExecutionDeadlineOperationResult<DaemonLifecycleObservation?>.Success(null);
        }

        var lifecycleReadOperation = await ExecutionDeadlineOperation.ExecuteAsync(
                deadline,
                cancellationToken,
                $"Timed out before reading daemon lifecycle recovery evidence. ProcessId={session.ProcessId}.",
                $"Timed out while reading daemon lifecycle recovery evidence. ProcessId={session.ProcessId}.",
                token => daemonLifecycleStore.ReadAsync(
                    unityProject.RepositoryRoot,
                    unityProject.ProjectFingerprint,
                    token))
            .ConfigureAwait(false);
        if (!lifecycleReadOperation.IsSuccess)
        {
            return ExecutionDeadlineOperationResult<DaemonLifecycleObservation?>.Failure(
                lifecycleReadOperation.Error!);
        }

        var lifecycleReadResult = lifecycleReadOperation.Value!;
        var observation = lifecycleReadResult.Observation;
        if (!lifecycleReadResult.IsSuccess
            || !lifecycleReadResult.Exists
            || observation is null
            || !DaemonLifecycleObservationAvailability.IsUsableForSession(
                observation,
                session,
                processIdentityAssessor,
                deadline.Clock))
        {
            return ExecutionDeadlineOperationResult<DaemonLifecycleObservation?>.Success(null);
        }

        return ExecutionDeadlineOperationResult<DaemonLifecycleObservation?>.Success(observation);
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

    private static bool MatchesExpectedSessionIdentity (
        DaemonSession expectedSession,
        DaemonSession candidate,
        ResolvedUnityProjectContext unityProject)
    {
        if (expectedSession.ProjectFingerprint != unityProject.ProjectFingerprint
            || candidate.ProjectFingerprint != unityProject.ProjectFingerprint
            || candidate.ProcessId != expectedSession.ProcessId
            || candidate.EditorMode != expectedSession.EditorMode
            || candidate.OwnerKind != expectedSession.OwnerKind
            || candidate.EditorInstanceId != expectedSession.EditorInstanceId)
        {
            return false;
        }

        if (expectedSession.ProcessStartedAtUtc is not DateTimeOffset expectedProcessStartedAtUtc)
        {
            return candidate.ProcessStartedAtUtc is null;
        }

        return candidate.ProcessStartedAtUtc is DateTimeOffset candidateProcessStartedAtUtc
            && DaemonProcessStartTimeMatcher.Matches(
                candidateProcessStartedAtUtc,
                expectedProcessStartedAtUtc);
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
