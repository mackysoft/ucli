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
    /// <summary> Executes and observes a Unity Editor Play Mode exit transition. </summary>
    internal sealed class PlayExitTransitionRunner
    {
        private const int RejectedPlayingObservationThreshold = 2;
        private const int StoppedWithoutGenerationChangeObservationThreshold = 2;

        private readonly IServerVersionProvider serverVersionProvider;
        private readonly IUnityEditorReadinessGate readinessGate;
        private readonly IpcProjectIdentity projectIdentity;
        private readonly IUnityEditorUpdateAwaiter editorUpdateAwaiter;
        private readonly IUnityPlayModeController playModeController;
        private readonly IDaemonLogger daemonLogger;
        private readonly IUnityMutationLaneControl mutationLaneControl;

        /// <summary> Initializes a new instance of the <see cref="PlayExitTransitionRunner" /> class. </summary>
        /// <param name="serverVersionProvider"> The server-version provider dependency. </param>
        /// <param name="readinessGate"> The Unity Editor observation provider dependency. </param>
        /// <param name="projectIdentity"> The project identity served by this IPC host. </param>
        /// <param name="editorUpdateAwaiter"> The editor update awaiter dependency. </param>
        /// <param name="playModeController"> The Play Mode controller dependency. </param>
        /// <param name="daemonLogger"> The daemon logger dependency. </param>
        public PlayExitTransitionRunner (
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

        /// <summary> Executes Play Mode exit and waits until Unity reports a ready edit-mode snapshot. </summary>
        /// <param name="recoverableContext"> The persisted operation context used to resume after domain reload. </param>
        /// <param name="cancellation"> The cancellation state propagated by the IPC request. </param>
        /// <returns> The structured transition result. </returns>
        public async Task<PlayExitTransitionExecutionResult> ExitAsync (
            RecoverableIpcOperationContext? recoverableContext,
            IpcRequestCancellation cancellation)
        {
            cancellation.Token.ThrowIfCancellationRequested();
            var before = CaptureObservation();

            if (recoverableContext != null && recoverableContext.HasOperationRecord)
            {
                if (!TryReadPendingExit(recoverableContext, out var pendingBefore, out var pendingReadErrorMessage))
                {
                    return CreateFailure(
                        PlayModeErrorCodes.PlayModeStateUnknown,
                        $"Recoverable Play Mode exit state is invalid. {pendingReadErrorMessage}",
                        before,
                        before,
                        IpcApplicationState.Unknown);
                }

                return await ResumePendingExitAsync(
                    pendingBefore,
                    before,
                    cancellation);
            }

            var preconditionFailure = ValidatePreconditions(before);
            if (preconditionFailure != null)
            {
                return preconditionFailure;
            }

            if (IsStoppedPlayModeSnapshot(before))
            {
                return CreateSuccess(IpcPlayTransitionOutcome.AlreadyExited, before, before);
            }

            // NOTE: This must be persisted before Unity is asked to exit Play Mode.
            // Leaving Play Mode can trigger domain reload before this daemon can respond.
            var persistFailure = await TryPersistPendingExitAsync(
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
                playModeController.ExitPlayMode();
            }
            catch (Exception exception)
            {
                CompleteOrTrackMutationSafety(mutationActivity, isKnownSafe: false);
                return CreateFailure(
                    PlayModeErrorCodes.PlayModeExitRejected,
                    $"Unity rejected Play Mode exit. {exception.Message}",
                    before,
                    before,
                    IpcApplicationState.NotApplied);
            }

            try
            {
                var result = await ObserveRequestedExitAsync(
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

        private async Task<PlayExitTransitionExecutionResult> ResumePendingExitAsync (
            IpcUnityEditorObservation pendingBefore,
            IpcUnityEditorObservation current,
            IpcRequestCancellation cancellation)
        {
            if (IsRecoverablePendingExit(pendingBefore, current))
            {
                return CreateSuccess(IpcPlayTransitionOutcome.Exited, pendingBefore, current);
            }

            var mutationActivity = mutationLaneControl.BeginMutation();
            try
            {
                var result = await ObserveRequestedExitAsync(
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
                    "A Play Mode exit transition outlived its request.",
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
                    if (stableObservations >= RejectedPlayingObservationThreshold)
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

        private static bool IsKnownSafeTerminalResult (PlayExitTransitionExecutionResult result)
        {
            return result.IsSuccess
                || result.Error?.Code == PlayModeErrorCodes.PlayModeExitRejected;
        }

        private async Task<PlayExitTransitionExecutionResult> ObserveRequestedExitAsync (
            IpcUnityEditorObservation before,
            IpcUnityEditorObservation initialObserved,
            bool classifyInitialObservation,
            IpcRequestCancellation cancellation)
        {
            var observed = initialObserved;
            var playingObservations = 0;
            var stoppedWithoutGenerationChangeObservations = 0;
            try
            {
                if (classifyInitialObservation)
                {
                    var initialFailure = ClassifyObservedFailure(before, observed, ref playingObservations, ref stoppedWithoutGenerationChangeObservations);
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

                    if (IsReadyStoppedSnapshot(observed) && HasGenerationChanged(before, observed))
                    {
                        return CreateSuccess(IpcPlayTransitionOutcome.Exited, before, observed);
                    }

                    var observedFailure = ClassifyObservedFailure(before, observed, ref playingObservations, ref stoppedWithoutGenerationChangeObservations);
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

        private bool TryReadPendingExit (
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

            if (!recoverableContext.TryReadPendingPayload<PlayExitRecoveryPayload>(out var recoveryPayload, out var pendingPayloadErrorMessage))
            {
                errorMessage = string.IsNullOrWhiteSpace(pendingPayloadErrorMessage)
                    ? "Pending Play Mode exit payload is missing."
                    : pendingPayloadErrorMessage;
                if (!string.IsNullOrWhiteSpace(pendingPayloadErrorMessage))
                {
                    daemonLogger.Warning(
                        DaemonLogCategories.Lifecycle,
                        $"Play Mode exit pending transition read failed. {pendingPayloadErrorMessage}");
                }

                return false;
            }

            before = recoveryPayload.Before;
            if (before == null)
            {
                errorMessage = "Pending Play Mode exit before snapshot is missing.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        private async Task<PlayExitTransitionExecutionResult> TryPersistPendingExitAsync (
            RecoverableIpcOperationContext? recoverableContext,
            IpcUnityEditorObservation before,
            CancellationToken cancellationToken)
        {
            if (recoverableContext == null)
            {
                return null;
            }

            var result = await recoverableContext.MarkPendingAsync(
                new PlayExitRecoveryPayload(before),
                cancellationToken);
            return result.IsSuccess
                ? null
                : CreateFailure(
                    PlayModeErrorCodes.PlayModeExitRejected,
                    $"Unity Play Mode exit could not persist transition recovery state. {result.ErrorMessage}",
                    before,
                    before,
                    IpcApplicationState.NotApplied);
        }

        private PlayExitTransitionExecutionResult ValidatePreconditions (IpcUnityEditorObservation before)
        {
            if (before.State.EditorMode != DaemonEditorMode.Gui)
            {
                return CreateFailure(
                    PlayModeErrorCodes.PlayModeRequiresGuiEditor,
                    "Play Mode exit requires a GUI Editor session.",
                    before,
                    before,
                    IpcApplicationState.NotApplied);
            }

            if (before.State.PlayMode == null || IsUnknownPlayMode(before))
            {
                return CreateFailure(
                    PlayModeErrorCodes.PlayModeStateUnknown,
                    "Unity Play Mode state is unknown before exiting Play Mode.",
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

            if (IsStoppedPlayModeSnapshot(before))
            {
                return null;
            }

            if (!IsEnteredSnapshot(before))
            {
                return CreateFailure(
                    PlayModeErrorCodes.PlayModeTransitionBlocked,
                    $"Unity Play Mode exit is blocked by lifecycleState={TextVocabulary.GetText(before.State.LifecycleState)}.",
                    before,
                    before,
                    IpcApplicationState.NotApplied);
            }

            return null;
        }

        private PlayExitTransitionExecutionResult ClassifyObservedFailure (
            IpcUnityEditorObservation before,
            IpcUnityEditorObservation observed,
            ref int playingObservations,
            ref int stoppedWithoutGenerationChangeObservations)
        {
            if (observed.State.PlayMode == null || IsUnknownPlayMode(observed))
            {
                return CreateFailure(
                    PlayModeErrorCodes.PlayModeStateUnknown,
                    "Unity Play Mode state became unknown while exiting Play Mode.",
                    before,
                    observed,
                    IpcApplicationState.Unknown);
            }

            TryReadPlayModeSnapshot(
                observed,
                out _,
                out var observedPlayModeState,
                out var observedPlayModeTransition);

            if (observedPlayModeState == IpcPlayModeState.Exiting
                || observedPlayModeTransition == IpcPlayModeTransition.Exiting)
            {
                playingObservations = 0;
                stoppedWithoutGenerationChangeObservations = 0;
                return null;
            }

            if (observedPlayModeState == IpcPlayModeState.Entering
                || observedPlayModeTransition == IpcPlayModeTransition.Entering)
            {
                return CreateFailure(
                    PlayModeErrorCodes.PlayModeAlreadyChanging,
                    "Unity Play Mode started entering while exit was requested.",
                    before,
                    observed,
                    IpcApplicationState.Unknown);
            }

            if (IsStoppedPlayModeSnapshot(observed) && HasGenerationChanged(before, observed))
            {
                if (IsExitWaitLifecycle(observed))
                {
                    playingObservations = 0;
                    stoppedWithoutGenerationChangeObservations = 0;
                    return null;
                }

                return CreateFailure(
                    PlayModeErrorCodes.PlayModeTransitionBlocked,
                    $"Unity Play Mode exit completed but lifecycleState={TextVocabulary.GetText(observed.State.LifecycleState)} blocked readiness.",
                    before,
                    observed,
                    IpcApplicationState.Applied);
            }

            if (!IsExitWaitLifecycle(observed))
            {
                return CreateFailure(
                    PlayModeErrorCodes.PlayModeTransitionBlocked,
                    $"Unity Play Mode exit was blocked by lifecycleState={TextVocabulary.GetText(observed.State.LifecycleState)}.",
                    before,
                    observed,
                    IpcApplicationState.Unknown);
            }

            if (IsEnteredSnapshot(observed))
            {
                playingObservations++;
                if (playingObservations >= RejectedPlayingObservationThreshold)
                {
                    return CreateFailure(
                        PlayModeErrorCodes.PlayModeExitRejected,
                        "Unity did not accept the Play Mode exit request.",
                        before,
                        observed,
                        IpcApplicationState.NotApplied);
                }
            }
            else
            {
                playingObservations = 0;
            }

            if (IsStoppedPlayModeSnapshot(observed) && !HasGenerationChanged(before, observed))
            {
                stoppedWithoutGenerationChangeObservations++;
                if (stoppedWithoutGenerationChangeObservations >= StoppedWithoutGenerationChangeObservationThreshold)
                {
                    return CreateFailure(
                        PlayModeErrorCodes.PlayModeStateUnknown,
                        "Unity Play Mode stopped without advancing generations.playModeGeneration.",
                        before,
                        observed,
                        IpcApplicationState.Unknown);
                }
            }
            else
            {
                stoppedWithoutGenerationChangeObservations = 0;
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

        private static PlayExitTransitionExecutionResult CreateSuccess (
            IpcPlayTransitionOutcome result,
            IpcUnityEditorObservation before,
            IpcUnityEditorObservation after)
        {
            return PlayExitTransitionExecutionResult.Success(new IpcPlayTransitionResponse(
                new IpcPlayTransitionResult(
                    Transition: IpcPlayTransitionCommand.Exit,
                    Result: result,
                    Before: before,
                    After: after,
                    Observed: null,
                    ApplicationState: null)));
        }

        private static PlayExitTransitionExecutionResult CreateFailure (
            UcliCode code,
            string message,
            IpcUnityEditorObservation before,
            IpcUnityEditorObservation observed,
            IpcApplicationState applicationState)
        {
            var response = new IpcPlayTransitionResponse(
                new IpcPlayTransitionResult(
                    Transition: IpcPlayTransitionCommand.Exit,
                    Result: IpcPlayTransitionOutcome.Blocked,
                    Before: before,
                    After: null,
                    Observed: observed,
                    ApplicationState: applicationState));
            return PlayExitTransitionExecutionResult.Failure(response, new IpcError(code, message, null));
        }

        private static PlayExitTransitionExecutionResult CreateTimeout (
            IpcUnityEditorObservation before,
            IpcUnityEditorObservation observed)
        {
            var response = new IpcPlayTransitionResponse(
                new IpcPlayTransitionResult(
                    Transition: IpcPlayTransitionCommand.Exit,
                    Result: IpcPlayTransitionOutcome.Timeout,
                    Before: before,
                    After: null,
                    Observed: observed,
                    ApplicationState: IpcApplicationState.Indeterminate));
            return PlayExitTransitionExecutionResult.Failure(
                response,
                new IpcError(
                    PlayModeErrorCodes.PlayModeTransitionTimeout,
                    "Unity Play Mode exit reached its request deadline.",
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
            return IsStoppedPlayModeSnapshot(snapshot)
                && snapshot.State.LifecycleState == IpcEditorLifecycleState.Ready;
        }

        private static bool IsStoppedPlayModeSnapshot (IpcUnityEditorObservation snapshot)
        {
            return TryReadPlayModeSnapshot(
                    snapshot,
                    out var playMode,
                    out var playModeState,
                    out var playModeTransition)
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

        private static bool IsExitWaitLifecycle (IpcUnityEditorObservation snapshot)
        {
            return UnityEditorExecutionReadinessPolicy.IsWaitableState(snapshot.State.LifecycleState)
                || snapshot.State.LifecycleState is IpcEditorLifecycleState.Ready or IpcEditorLifecycleState.PlayMode;
        }

        private static bool IsRecoverablePendingExit (
            IpcUnityEditorObservation pendingBefore,
            IpcUnityEditorObservation current)
        {
            return pendingBefore != null
                && current != null
                && IsEnteredSnapshot(pendingBefore)
                && IsReadyStoppedSnapshot(current)
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
