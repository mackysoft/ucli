using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Text.Vocabularies;
using TextVocabulary = MackySoft.Text.Vocabularies.Vocabulary;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Unity.Runtime;

#nullable enable annotations

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Executes and observes a Unity Editor Play Mode enter transition. </summary>
    internal sealed class PlayEnterTransitionRunner
    {
        private const int RejectedStoppedObservationThreshold = 2;

        private readonly IServerVersionProvider serverVersionProvider;
        private readonly IUnityEditorReadinessGate readinessGate;
        private readonly IpcProjectIdentity projectIdentity;
        private readonly IUnityEditorUpdateAwaiter editorUpdateAwaiter;
        private readonly IUnityPlayModeController playModeController;
        private readonly IDaemonLogger daemonLogger;
        private readonly IUnityMutationLaneControl mutationLaneControl;

        /// <summary> Initializes a new instance of the <see cref="PlayEnterTransitionRunner" /> class. </summary>
        /// <param name="serverVersionProvider"> The server-version provider dependency. </param>
        /// <param name="readinessGate"> The Unity Editor observation provider dependency. </param>
        /// <param name="projectIdentity"> The project identity served by this IPC host. </param>
        /// <param name="editorUpdateAwaiter"> The editor update awaiter dependency. </param>
        /// <param name="playModeController"> The Play Mode controller dependency. </param>
        /// <param name="daemonLogger"> The daemon logger dependency. </param>
        public PlayEnterTransitionRunner (
            IServerVersionProvider serverVersionProvider,
            IUnityEditorReadinessGate readinessGate,
            IpcProjectIdentity projectIdentity,
            IUnityEditorUpdateAwaiter editorUpdateAwaiter,
            IUnityPlayModeController playModeController,
            IDaemonLogger daemonLogger,
            IUnityMutationLaneControl mutationLaneControl)
        {
            this.serverVersionProvider = serverVersionProvider ?? throw new ArgumentNullException(nameof(serverVersionProvider));
            this.readinessGate = readinessGate ?? throw new ArgumentNullException(nameof(readinessGate));
            this.projectIdentity = projectIdentity ?? throw new ArgumentNullException(nameof(projectIdentity));
            this.editorUpdateAwaiter = editorUpdateAwaiter ?? throw new ArgumentNullException(nameof(editorUpdateAwaiter));
            this.playModeController = playModeController ?? throw new ArgumentNullException(nameof(playModeController));
            this.daemonLogger = daemonLogger ?? throw new ArgumentNullException(nameof(daemonLogger));
            this.mutationLaneControl = mutationLaneControl ?? throw new ArgumentNullException(nameof(mutationLaneControl));
        }

        /// <summary> Executes Play Mode enter and waits until Unity reports an entered snapshot. </summary>
        /// <param name="recoverableContext"> The persisted operation context used to resume after domain reload. </param>
        /// <param name="cancellation"> The cancellation state propagated by the IPC request. </param>
        /// <returns> The structured transition result. </returns>
        public async Task<PlayEnterTransitionExecutionResult> EnterAsync (
            RecoverableIpcOperationContext? recoverableContext,
            IpcRequestCancellation cancellation)
        {
            cancellation.Token.ThrowIfCancellationRequested();
            var before = CaptureObservation();

            if (recoverableContext != null && recoverableContext.HasOperationRecord)
            {
                if (!TryReadPendingEnter(recoverableContext, out var pendingBefore, out var pendingReadErrorMessage))
                {
                    return CreateFailure(
                        PlayModeErrorCodes.PlayModeStateUnknown,
                        $"Recoverable Play Mode enter state is invalid. {pendingReadErrorMessage}",
                        before,
                        before,
                        IpcApplicationState.Unknown);
                }

                return await ResumePendingEnterAsync(
                    pendingBefore,
                    before,
                    cancellation);
            }

            var preconditionFailure = ValidatePreconditions(before);
            if (preconditionFailure != null)
            {
                return preconditionFailure;
            }

            if (IsEnteredSnapshot(before))
            {
                return CreateSuccess(IpcPlayTransitionOutcome.AlreadyEntered, before, before);
            }

            // NOTE: This must be persisted before Unity is asked to enter Play Mode.
            // Entering Play Mode can trigger domain reload before this daemon can respond.
            var persistFailure = await TryPersistPendingEnterAsync(
                recoverableContext,
                before,
                cancellation.Token);
            if (persistFailure != null)
            {
                return persistFailure;
            }

            var mutationActivity = mutationLaneControl.BeginMutation();
            try
            {
                playModeController.EnterPlayMode();
            }
            catch (Exception exception)
            {
                CompleteOrTrackMutationSafety(mutationActivity, isKnownSafe: false);
                return CreateFailure(
                    PlayModeErrorCodes.PlayModeEnterRejected,
                    $"Unity rejected Play Mode enter. {exception.Message}",
                    before,
                    before,
                    IpcApplicationState.NotApplied);
            }

            try
            {
                var result = await ObserveRequestedEnterAsync(
                    before,
                    before,
                    classifyInitialObservation: false,
                    cancellation);
                CompleteOrTrackMutationSafety(mutationActivity, IsKnownSafeTerminalResult(result));
                return result;
            }
            catch
            {
                CompleteOrTrackMutationSafety(mutationActivity, isKnownSafe: false);
                throw;
            }
        }

        private async Task<PlayEnterTransitionExecutionResult> ResumePendingEnterAsync (
            IpcUnityEditorObservation pendingBefore,
            IpcUnityEditorObservation current,
            IpcRequestCancellation cancellation)
        {
            if (IsRecoverablePendingEnter(pendingBefore, current))
            {
                return CreateSuccess(IpcPlayTransitionOutcome.Entered, pendingBefore, current);
            }

            var mutationActivity = mutationLaneControl.BeginMutation();
            try
            {
                var result = await ObserveRequestedEnterAsync(
                    pendingBefore,
                    current,
                    classifyInitialObservation: true,
                    cancellation);
                CompleteOrTrackMutationSafety(mutationActivity, IsKnownSafeTerminalResult(result));
                return result;
            }
            catch
            {
                CompleteOrTrackMutationSafety(mutationActivity, isKnownSafe: false);
                throw;
            }
        }

        private void CompleteOrTrackMutationSafety (
            IUnityMutationActivity mutationActivity,
            bool isKnownSafe)
        {
            if (isKnownSafe)
            {
                mutationActivity.Complete();
                return;
            }

            var safetyTask = WaitForMutationSafetyAsync();
            _ = safetyTask.ContinueWith(
                static (completedTask, state) =>
                {
                    _ = completedTask.Exception;
                    if (completedTask.Status == TaskStatus.RanToCompletion)
                    {
                        ((IUnityMutationActivity)state).Complete();
                    }
                },
                mutationActivity,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            if (!safetyTask.IsCompleted)
            {
                mutationLaneControl.Quarantine(
                    "A Play Mode enter transition outlived its request.",
                    safetyTask);
            }
        }

        private async Task WaitForMutationSafetyAsync ()
        {
            var stableObservations = 0;
            while (true)
            {
                var observed = CaptureObservation();
                if (IsEnteredSnapshot(observed) || IsReadyStoppedSnapshot(observed))
                {
                    stableObservations++;
                    if (stableObservations >= RejectedStoppedObservationThreshold)
                    {
                        return;
                    }
                }
                else
                {
                    stableObservations = 0;
                }

                await Task.Yield();
                await editorUpdateAwaiter.WaitForNextUpdateAsync(CancellationToken.None);
            }
        }

        private static bool IsKnownSafeTerminalResult (PlayEnterTransitionExecutionResult result)
        {
            return result.IsSuccess
                || result.Error?.Code == PlayModeErrorCodes.PlayModeEnterRejected;
        }

        private async Task<PlayEnterTransitionExecutionResult> ObserveRequestedEnterAsync (
            IpcUnityEditorObservation before,
            IpcUnityEditorObservation initialObserved,
            bool classifyInitialObservation,
            IpcRequestCancellation cancellation)
        {
            var observed = initialObserved;
            var stoppedObservations = 0;
            try
            {
                if (classifyInitialObservation)
                {
                    var initialFailure = ClassifyObservedFailure(before, observed, ref stoppedObservations);
                    if (initialFailure != null)
                    {
                        return initialFailure;
                    }
                }

                while (true)
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    await editorUpdateAwaiter.WaitForNextUpdateAsync(cancellation.Token);
                    observed = CaptureObservation();

                    if (IsEnteredSnapshot(observed) && HasGenerationChanged(before, observed))
                    {
                        return CreateSuccess(IpcPlayTransitionOutcome.Entered, before, observed);
                    }

                    var observedFailure = ClassifyObservedFailure(before, observed, ref stoppedObservations);
                    if (observedFailure != null)
                    {
                        return observedFailure;
                    }
                }
            }
            catch (OperationCanceledException) when (
                cancellation.Reason == IpcRequestCancellationReason.ExecutionDeadline)
            {
                return CreateTimeout(before, observed);
            }
        }

        private bool TryReadPendingEnter (
            RecoverableIpcOperationContext? recoverableContext,
            out IpcUnityEditorObservation before,
            out string errorMessage)
        {
            before = null;
            if (recoverableContext == null)
            {
                errorMessage = null;
                return false;
            }

            if (!recoverableContext.TryReadPendingPayload<PlayEnterRecoveryPayload>(out var recoveryPayload, out var pendingPayloadErrorMessage))
            {
                errorMessage = string.IsNullOrWhiteSpace(pendingPayloadErrorMessage)
                    ? "Pending Play Mode enter payload is missing."
                    : pendingPayloadErrorMessage;
                if (!string.IsNullOrWhiteSpace(pendingPayloadErrorMessage))
                {
                    daemonLogger.Warning(
                        DaemonLogCategories.Lifecycle,
                        $"Play Mode enter pending transition read failed. {pendingPayloadErrorMessage}");
                }

                return false;
            }

            before = recoveryPayload.Before;
            if (before == null)
            {
                errorMessage = "Pending Play Mode enter before snapshot is missing.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        private async Task<PlayEnterTransitionExecutionResult> TryPersistPendingEnterAsync (
            RecoverableIpcOperationContext? recoverableContext,
            IpcUnityEditorObservation before,
            CancellationToken cancellationToken)
        {
            if (recoverableContext == null)
            {
                return null;
            }

            var result = await recoverableContext.MarkPendingAsync(
                new PlayEnterRecoveryPayload(before),
                cancellationToken);
            return result.IsSuccess
                ? null
                : CreateFailure(
                    PlayModeErrorCodes.PlayModeEnterRejected,
                    $"Unity Play Mode enter could not persist transition recovery state. {result.ErrorMessage}",
                    before,
                    before,
                    IpcApplicationState.NotApplied);
        }

        private PlayEnterTransitionExecutionResult ValidatePreconditions (IpcUnityEditorObservation before)
        {
            if (before.State.EditorMode != DaemonEditorMode.Gui)
            {
                return CreateFailure(
                    PlayModeErrorCodes.PlayModeRequiresGuiEditor,
                    "Play Mode enter requires a GUI Editor session.",
                    before,
                    before,
                    IpcApplicationState.NotApplied);
            }

            if (before.State.PlayMode == null || IsUnknownPlayMode(before))
            {
                return CreateFailure(
                    PlayModeErrorCodes.PlayModeStateUnknown,
                    "Unity Play Mode state is unknown before entering Play Mode.",
                    before,
                    before,
                    IpcApplicationState.Unknown);
            }

            if (IsPlayModeChanging(before))
            {
                return CreateFailure(
                    PlayModeErrorCodes.PlayModeAlreadyChanging,
                    "Unity Play Mode is already changing.",
                    before,
                    before,
                    IpcApplicationState.NotApplied);
            }

            if (IsEnteredSnapshot(before))
            {
                return null;
            }

            if (!IsReadyStoppedSnapshot(before))
            {
                return CreateFailure(
                    PlayModeErrorCodes.PlayModeTransitionBlocked,
                    $"Unity Play Mode enter is blocked by lifecycleState={TextVocabulary.GetText(before.State.LifecycleState)}.",
                    before,
                    before,
                    IpcApplicationState.NotApplied);
            }

            return null;
        }

        private PlayEnterTransitionExecutionResult ClassifyObservedFailure (
            IpcUnityEditorObservation before,
            IpcUnityEditorObservation observed,
            ref int stoppedObservations)
        {
            if (IsEnterTransitionLifecycle(observed.State.LifecycleState))
            {
                stoppedObservations = 0;
                return null;
            }

            if (observed.State.PlayMode == null || IsUnknownPlayMode(observed))
            {
                return CreateFailure(
                    PlayModeErrorCodes.PlayModeStateUnknown,
                    "Unity Play Mode state became unknown while entering Play Mode.",
                    before,
                    observed,
                    IpcApplicationState.Unknown);
            }

            TryReadPlayModeSnapshot(
                observed,
                out _,
                out var observedPlayModeState,
                out var observedPlayModeTransition);
            if (observedPlayModeState == IpcPlayModeState.Entering
                || observedPlayModeTransition == IpcPlayModeTransition.Entering)
            {
                stoppedObservations = 0;
                return null;
            }

            if (observedPlayModeState == IpcPlayModeState.Exiting
                || observedPlayModeTransition == IpcPlayModeTransition.Exiting)
            {
                return CreateFailure(
                    PlayModeErrorCodes.PlayModeAlreadyChanging,
                    "Unity Play Mode started exiting while enter was requested.",
                    before,
                    observed,
                    IpcApplicationState.Unknown);
            }

            if (!IsReadyOrPlayModeLifecycle(observed))
            {
                return CreateFailure(
                    PlayModeErrorCodes.PlayModeTransitionBlocked,
                    $"Unity Play Mode enter was blocked by lifecycleState={TextVocabulary.GetText(observed.State.LifecycleState)}.",
                    before,
                    observed,
                    IpcApplicationState.Unknown);
            }

            if (IsReadyStoppedSnapshot(observed))
            {
                stoppedObservations++;
                if (stoppedObservations >= RejectedStoppedObservationThreshold)
                {
                    return CreateFailure(
                        PlayModeErrorCodes.PlayModeEnterRejected,
                        "Unity did not accept the Play Mode enter request.",
                        before,
                        observed,
                        IpcApplicationState.NotApplied);
                }
            }
            else
            {
                stoppedObservations = 0;
            }

            return null;
        }

        private IpcUnityEditorObservation CaptureObservation ()
        {
            return UnityLifecycleResponseFactory.Create(
                projectIdentity,
                serverVersionProvider.GetVersion(),
                readinessGate.CaptureObservation());
        }

        private static PlayEnterTransitionExecutionResult CreateSuccess (
            IpcPlayTransitionOutcome result,
            IpcUnityEditorObservation before,
            IpcUnityEditorObservation after)
        {
            return PlayEnterTransitionExecutionResult.Success(new IpcPlayTransitionResponse(
                new IpcPlayTransitionResult(
                    Transition: IpcPlayTransitionCommand.Enter,
                    Result: result,
                    Before: before,
                    After: after,
                    Observed: null,
                    ApplicationState: null)));
        }

        private static PlayEnterTransitionExecutionResult CreateFailure (
            UcliCode code,
            string message,
            IpcUnityEditorObservation before,
            IpcUnityEditorObservation observed,
            IpcApplicationState applicationState)
        {
            var response = new IpcPlayTransitionResponse(
                new IpcPlayTransitionResult(
                    Transition: IpcPlayTransitionCommand.Enter,
                    Result: IpcPlayTransitionOutcome.Blocked,
                    Before: before,
                    After: null,
                    Observed: observed,
                    ApplicationState: applicationState));
            return PlayEnterTransitionExecutionResult.Failure(response, new IpcError(code, message, null));
        }

        private static PlayEnterTransitionExecutionResult CreateTimeout (
            IpcUnityEditorObservation before,
            IpcUnityEditorObservation observed)
        {
            var response = new IpcPlayTransitionResponse(
                new IpcPlayTransitionResult(
                    Transition: IpcPlayTransitionCommand.Enter,
                    Result: IpcPlayTransitionOutcome.Timeout,
                    Before: before,
                    After: null,
                    Observed: observed,
                    ApplicationState: IpcApplicationState.Indeterminate));
            return PlayEnterTransitionExecutionResult.Failure(
                response,
                new IpcError(
                    PlayModeErrorCodes.PlayModeTransitionTimeout,
                    "Unity Play Mode enter reached its request deadline.",
                    null));
        }

        private static bool IsEnteredSnapshot (IpcUnityEditorObservation snapshot)
        {
            return TryReadPlayModeSnapshot(
                    snapshot,
                    out var playMode,
                    out var playModeState,
                    out var playModeTransition)
                && snapshot.State.LifecycleState == IpcEditorLifecycleState.PlayMode
                && playModeState == IpcPlayModeState.Playing
                && playModeTransition == IpcPlayModeTransition.None
                && playMode.IsPlaying;
        }

        private static bool HasGenerationChanged (
            IpcUnityEditorObservation before,
            IpcUnityEditorObservation after)
        {
            return before.State.Generations.PlayModeGeneration
                != after.State.Generations.PlayModeGeneration;
        }

        private static bool IsReadyStoppedSnapshot (IpcUnityEditorObservation snapshot)
        {
            return TryReadPlayModeSnapshot(
                    snapshot,
                    out var playMode,
                    out var playModeState,
                    out var playModeTransition)
                && snapshot.State.LifecycleState == IpcEditorLifecycleState.Ready
                && playModeState == IpcPlayModeState.Stopped
                && playModeTransition == IpcPlayModeTransition.None
                && !playMode.IsPlaying
                && !playMode.IsPlayingOrWillChangePlaymode;
        }

        private static bool IsPlayModeChanging (IpcUnityEditorObservation snapshot)
        {
            return TryReadPlayModeSnapshot(
                    snapshot,
                    out _,
                    out var playModeState,
                    out var playModeTransition)
                && (playModeState == IpcPlayModeState.Entering
                    || playModeState == IpcPlayModeState.Exiting
                    || playModeTransition == IpcPlayModeTransition.Entering
                    || playModeTransition == IpcPlayModeTransition.Exiting);
        }

        private static bool IsUnknownPlayMode (IpcUnityEditorObservation snapshot)
        {
            if (!TryReadPlayModeSnapshot(
                    snapshot,
                    out _,
                    out var playModeState,
                    out _))
            {
                return true;
            }

            return playModeState == IpcPlayModeState.Unknown;
        }

        private static bool IsReadyOrPlayModeLifecycle (IpcUnityEditorObservation snapshot)
        {
            return snapshot.State.LifecycleState is IpcEditorLifecycleState.Ready or IpcEditorLifecycleState.PlayMode;
        }

        private static bool IsEnterTransitionLifecycle (IpcEditorLifecycleState lifecycleState)
        {
            return lifecycleState is IpcEditorLifecycleState.Starting
                or IpcEditorLifecycleState.Recovering
                or IpcEditorLifecycleState.Compiling
                or IpcEditorLifecycleState.DomainReloading
                or IpcEditorLifecycleState.Reimporting;
        }

        private static bool IsRecoverablePendingEnter (
            IpcUnityEditorObservation pendingBefore,
            IpcUnityEditorObservation current)
        {
            return pendingBefore != null
                && current != null
                && IsReadyStoppedSnapshot(pendingBefore)
                && IsEnteredSnapshot(current)
                && pendingBefore.ProjectFingerprint == current.ProjectFingerprint
                && HasGenerationChanged(pendingBefore, current);
        }

        private static bool TryReadPlayModeSnapshot (
            IpcUnityEditorObservation snapshot,
            out IpcPlayModeSnapshot playMode,
            out IpcPlayModeState state,
            out IpcPlayModeTransition transition)
        {
            playMode = snapshot.State.PlayMode;
            state = default;
            transition = default;
            if (playMode == null)
            {
                return false;
            }

            state = playMode.State;
            transition = playMode.Transition;
            return true;
        }

    }
}
