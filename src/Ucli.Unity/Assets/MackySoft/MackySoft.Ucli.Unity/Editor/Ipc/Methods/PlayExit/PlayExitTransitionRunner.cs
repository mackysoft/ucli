using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Text.Vocabularies;
using TextVocabulary = MackySoft.Text.Vocabularies.Vocabulary;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Unity.Runtime;

#nullable enable annotations

using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Projects;
using MackySoft.Ucli.Contracts.Execution;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Executes and observes a Unity Editor Play Mode exit transition. </summary>
    internal sealed class PlayExitTransitionRunner
    {
        private const int RejectedPlayingObservationThreshold = 2;
        private const int StoppedWithoutGenerationChangeObservationThreshold = 2;

        private readonly IServerVersionProvider serverVersionProvider;
        private readonly IUnityEditorReadinessGate readinessGate;
        private readonly UnityProjectIdentity projectIdentity;
        private readonly IUnityEditorUpdateAwaiter editorUpdateAwaiter;
        private readonly IUnityPlayModeController playModeController;
        private readonly IUnityMutationLaneControl mutationLaneControl;

        /// <summary> Initializes a new instance of the <see cref="PlayExitTransitionRunner" /> class. </summary>
        /// <param name="serverVersionProvider"> The server-version provider dependency. </param>
        /// <param name="readinessGate"> The Unity Editor observation provider dependency. </param>
        /// <param name="projectIdentity"> The project identity served by this IPC host. </param>
        /// <param name="editorUpdateAwaiter"> The editor update awaiter dependency. </param>
        /// <param name="playModeController"> The Play Mode controller dependency. </param>
        /// <param name="mutationLaneControl"> The mutation-lane safety dependency. </param>
        public PlayExitTransitionRunner (
            IServerVersionProvider serverVersionProvider,
            IUnityEditorReadinessGate readinessGate,
            UnityProjectIdentity projectIdentity,
            IUnityEditorUpdateAwaiter editorUpdateAwaiter,
            IUnityPlayModeController playModeController,
            IUnityMutationLaneControl mutationLaneControl)
        {
            this.serverVersionProvider = serverVersionProvider ?? throw new ArgumentNullException(nameof(serverVersionProvider));
            this.readinessGate = readinessGate ?? throw new ArgumentNullException(nameof(readinessGate));
            this.projectIdentity = projectIdentity ?? throw new ArgumentNullException(nameof(projectIdentity));
            this.editorUpdateAwaiter = editorUpdateAwaiter ?? throw new ArgumentNullException(nameof(editorUpdateAwaiter));
            this.playModeController = playModeController ?? throw new ArgumentNullException(nameof(playModeController));
            this.mutationLaneControl = mutationLaneControl ?? throw new ArgumentNullException(nameof(mutationLaneControl));
        }

        /// <summary>
        /// Evaluates whether Play Mode exit is terminal or requires a durably admitted side effect.
        /// </summary>
        /// <param name="executionDeadlineCancellationToken"> Cancellation controlled only by the immutable execution deadline. </param>
        /// <returns> The preparation owned by the action handler. </returns>
        public PlayExitTransitionPreparation Prepare (
            CancellationToken executionDeadlineCancellationToken)
        {
            executionDeadlineCancellationToken.ThrowIfCancellationRequested();
            var before = CaptureObservation();

            var preconditionFailure = ValidatePreconditions(before);
            if (preconditionFailure != null)
            {
                return PlayExitTransitionPreparation.Terminal(preconditionFailure);
            }

            if (IsStoppedPlayModeSnapshot(before))
            {
                return PlayExitTransitionPreparation.Terminal(
                    CreateSuccess(PlayLifecycleTransitionOutcome.AlreadyExited, before, before));
            }

            return PlayExitTransitionPreparation.Issue(before);
        }

        /// <summary> Issues the already-admitted exit side effect and observes its terminal result. </summary>
        /// <param name="before"> The durable observation captured before side-effect admission. </param>
        /// <param name="executionDeadlineCancellationToken"> Cancellation controlled only by the immutable execution deadline. </param>
        /// <returns> The typed Play Mode exit result. </returns>
        public async Task<PlayExitTransitionExecutionResult> IssueAsync (
            UnityEditorObservation before,
            CancellationToken executionDeadlineCancellationToken)
        {
            if (before == null)
            {
                throw new ArgumentNullException(nameof(before));
            }

            executionDeadlineCancellationToken.ThrowIfCancellationRequested();
            var mutationActivity = mutationLaneControl.BeginMutation();
            try
            {
                executionDeadlineCancellationToken.ThrowIfCancellationRequested();
                playModeController.ExitPlayMode();
            }
            catch (OperationCanceledException) when (
                executionDeadlineCancellationToken.IsCancellationRequested)
            {
                mutationActivity.Complete();
                throw;
            }
            catch (UnityPlayModeTransitionException exception)
            {
                CompleteOrTrackMutationSafety(mutationActivity, isKnownSafe: false);
                return CreateFailure(
                    PlayModeErrorCodes.PlayModeExitRejected,
                    $"Unity rejected Play Mode exit. {exception.Message}",
                    before,
                    before,
                    ExecutionApplicationState.NotApplied);
            }
            catch
            {
                CompleteOrTrackMutationSafety(mutationActivity, isKnownSafe: false);
                throw;
            }

            try
            {
                var result = await ObserveRequestedExitAsync(
                    before,
                    before,
                    classifyInitialObservation: false,
                    executionDeadlineCancellationToken);
                CompleteOrTrackMutationSafety(mutationActivity, IsKnownSafeTerminalResult(result));
                return result;
            }
            catch
            {
                CompleteOrTrackMutationSafety(mutationActivity, isKnownSafe: false);
                throw;
            }
        }

        /// <summary>
        /// Resumes observation of an exit side effect that may already have crossed a domain reload.
        /// </summary>
        /// <param name="pendingBefore"> The durable observation captured before the original side effect. </param>
        /// <param name="executionDeadlineCancellationToken"> Cancellation controlled only by the immutable execution deadline. </param>
        /// <returns> The typed Play Mode exit result without reissuing the side effect. </returns>
        public async Task<PlayExitTransitionExecutionResult> RecoverAsync (
            UnityEditorObservation pendingBefore,
            CancellationToken executionDeadlineCancellationToken)
        {
            if (pendingBefore == null)
            {
                throw new ArgumentNullException(nameof(pendingBefore));
            }

            executionDeadlineCancellationToken.ThrowIfCancellationRequested();
            var current = CaptureObservation();
            if (IsRecoverablePendingExit(pendingBefore, current))
            {
                return CreateSuccess(PlayLifecycleTransitionOutcome.Exited, pendingBefore, current);
            }

            var mutationActivity = mutationLaneControl.BeginMutation();
            try
            {
                var result = await ObserveRequestedExitAsync(
                    pendingBefore,
                    current,
                    classifyInitialObservation: true,
                    executionDeadlineCancellationToken);
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
            UnityEditorObservation before,
            UnityEditorObservation initialObserved,
            bool classifyInitialObservation,
            CancellationToken executionDeadlineCancellationToken)
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
                    executionDeadlineCancellationToken.ThrowIfCancellationRequested();
                    await editorUpdateAwaiter.WaitForNextUpdateAsync(
                        executionDeadlineCancellationToken);
                    observed = CaptureObservation();

                    if (IsReadyStoppedSnapshot(observed) && HasGenerationChanged(before, observed))
                    {
                        return CreateSuccess(PlayLifecycleTransitionOutcome.Exited, before, observed);
                    }

                    var observedFailure = ClassifyObservedFailure(before, observed, ref playingObservations, ref stoppedWithoutGenerationChangeObservations);
                    if (observedFailure != null)
                    {
                        return observedFailure;
                    }
                }
            }
            catch (OperationCanceledException) when (
                executionDeadlineCancellationToken.IsCancellationRequested)
            {
                return CreateTimeout(before, observed);
            }
        }

        private PlayExitTransitionExecutionResult ValidatePreconditions (UnityEditorObservation before)
        {
            if (before.State.EditorMode != UnityEditorMode.Gui)
            {
                return CreateFailure(
                    PlayModeErrorCodes.PlayModeRequiresGuiEditor,
                    "Play Mode exit requires a GUI Editor session.",
                    before,
                    before,
                    ExecutionApplicationState.NotApplied);
            }

            if (before.State.PlayMode == null || IsUnknownPlayMode(before))
            {
                return CreateFailure(
                    PlayModeErrorCodes.PlayModeStateUnknown,
                    "Unity Play Mode state is unknown before exiting Play Mode.",
                    before,
                    before,
                    ExecutionApplicationState.Unknown);
            }

            if (IsPlayModeChanging(before))
            {
                return CreateFailure(
                    PlayModeErrorCodes.PlayModeAlreadyChanging,
                    "Unity Play Mode is already changing.",
                    before,
                    before,
                    ExecutionApplicationState.NotApplied);
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
                    ExecutionApplicationState.NotApplied);
            }

            return null;
        }

        private PlayExitTransitionExecutionResult ClassifyObservedFailure (
            UnityEditorObservation before,
            UnityEditorObservation observed,
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
                    ExecutionApplicationState.Unknown);
            }

            TryReadPlayModeSnapshot(
                observed,
                out _,
                out var observedPlayModeState,
                out var observedPlayModeTransition);

            if (observedPlayModeState == UnityEditorPlayModeState.Exiting
                || observedPlayModeTransition == UnityEditorPlayModeTransition.Exiting)
            {
                playingObservations = 0;
                stoppedWithoutGenerationChangeObservations = 0;
                return null;
            }

            if (observedPlayModeState == UnityEditorPlayModeState.Entering
                || observedPlayModeTransition == UnityEditorPlayModeTransition.Entering)
            {
                return CreateFailure(
                    PlayModeErrorCodes.PlayModeAlreadyChanging,
                    "Unity Play Mode started entering while exit was requested.",
                    before,
                    observed,
                    ExecutionApplicationState.Unknown);
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
                    ExecutionApplicationState.Applied);
            }

            if (!IsExitWaitLifecycle(observed))
            {
                return CreateFailure(
                    PlayModeErrorCodes.PlayModeTransitionBlocked,
                    $"Unity Play Mode exit was blocked by lifecycleState={TextVocabulary.GetText(observed.State.LifecycleState)}.",
                    before,
                    observed,
                    ExecutionApplicationState.Unknown);
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
                        ExecutionApplicationState.NotApplied);
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
                        ExecutionApplicationState.Unknown);
                }
            }
            else
            {
                stoppedWithoutGenerationChangeObservations = 0;
            }

            return null;
        }

        internal UnityEditorObservation CaptureObservation ()
        {
            return UnityLifecycleResponseFactory.Create(
                projectIdentity,
                serverVersionProvider.GetVersion(),
                readinessGate.CaptureObservation());
        }

        private static PlayExitTransitionExecutionResult CreateSuccess (
            PlayLifecycleTransitionOutcome result,
            UnityEditorObservation before,
            UnityEditorObservation after)
        {
            return PlayExitTransitionExecutionResult.Success(
                new PlayLifecycleTransitionResult(
                    Transition: PlayLifecycleTransitionCommand.Exit,
                    Result: result,
                    Before: before,
                    After: after,
                    Observed: null,
                    ApplicationState: null));
        }

        private static PlayExitTransitionExecutionResult CreateFailure (
            UcliCode code,
            string message,
            UnityEditorObservation before,
            UnityEditorObservation observed,
            ExecutionApplicationState applicationState)
        {
            var result = new PlayLifecycleTransitionResult(
                    Transition: PlayLifecycleTransitionCommand.Exit,
                    Result: PlayLifecycleTransitionOutcome.Blocked,
                    Before: before,
                    After: null,
                    Observed: observed,
                    ApplicationState: applicationState);
            return PlayExitTransitionExecutionResult.Failure(
                result,
                new PlayTransitionExecutionError(code, message));
        }

        private static PlayExitTransitionExecutionResult CreateTimeout (
            UnityEditorObservation before,
            UnityEditorObservation observed)
        {
            var result = new PlayLifecycleTransitionResult(
                    Transition: PlayLifecycleTransitionCommand.Exit,
                    Result: PlayLifecycleTransitionOutcome.Timeout,
                    Before: before,
                    After: null,
                    Observed: observed,
                    ApplicationState: ExecutionApplicationState.Indeterminate);
            return PlayExitTransitionExecutionResult.Failure(
                result,
                new PlayTransitionExecutionError(
                    PlayModeErrorCodes.PlayModeTransitionTimeout,
                    "Unity Play Mode exit reached its execution deadline."));
        }

        private static bool IsEnteredSnapshot (UnityEditorObservation snapshot)
        {
            return TryReadPlayModeSnapshot(
                    snapshot,
                    out var playMode,
                    out var playModeState,
                    out var playModeTransition)
                && snapshot.State.LifecycleState == UnityEditorLifecycleState.PlayMode
                && playModeState == UnityEditorPlayModeState.Playing
                && playModeTransition == UnityEditorPlayModeTransition.None
                && playMode.IsPlaying;
        }

        private static bool HasGenerationChanged (
            UnityEditorObservation before,
            UnityEditorObservation after)
        {
            return before.State.Generations.PlayModeGeneration
                != after.State.Generations.PlayModeGeneration;
        }

        private static bool IsReadyStoppedSnapshot (UnityEditorObservation snapshot)
        {
            return IsStoppedPlayModeSnapshot(snapshot)
                && snapshot.State.LifecycleState == UnityEditorLifecycleState.Ready;
        }

        private static bool IsStoppedPlayModeSnapshot (UnityEditorObservation snapshot)
        {
            return TryReadPlayModeSnapshot(
                    snapshot,
                    out var playMode,
                    out var playModeState,
                    out var playModeTransition)
                && playModeState == UnityEditorPlayModeState.Stopped
                && playModeTransition == UnityEditorPlayModeTransition.None
                && !playMode.IsPlaying
                && !playMode.IsPlayingOrWillChangePlaymode;
        }

        private static bool IsPlayModeChanging (UnityEditorObservation snapshot)
        {
            return TryReadPlayModeSnapshot(
                    snapshot,
                    out _,
                    out var playModeState,
                    out var playModeTransition)
                && (playModeState == UnityEditorPlayModeState.Entering
                    || playModeState == UnityEditorPlayModeState.Exiting
                    || playModeTransition == UnityEditorPlayModeTransition.Entering
                    || playModeTransition == UnityEditorPlayModeTransition.Exiting);
        }

        private static bool IsUnknownPlayMode (UnityEditorObservation snapshot)
        {
            if (!TryReadPlayModeSnapshot(
                    snapshot,
                    out _,
                    out var playModeState,
                    out _))
            {
                return true;
            }

            return playModeState == UnityEditorPlayModeState.Unknown;
        }

        private static bool IsExitWaitLifecycle (UnityEditorObservation snapshot)
        {
            return UnityEditorExecutionReadinessPolicy.IsWaitableState(snapshot.State.LifecycleState)
                || snapshot.State.LifecycleState is UnityEditorLifecycleState.Ready or UnityEditorLifecycleState.PlayMode;
        }

        private static bool IsRecoverablePendingExit (
            UnityEditorObservation pendingBefore,
            UnityEditorObservation current)
        {
            return pendingBefore != null
                && current != null
                && IsEnteredSnapshot(pendingBefore)
                && IsReadyStoppedSnapshot(current)
                && pendingBefore.ProjectFingerprint == current.ProjectFingerprint
                && HasGenerationChanged(pendingBefore, current);
        }

        private static bool TryReadPlayModeSnapshot (
            UnityEditorObservation snapshot,
            out UnityEditorPlayModeSnapshot playMode,
            out UnityEditorPlayModeState state,
            out UnityEditorPlayModeTransition transition)
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
