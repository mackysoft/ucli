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
    /// <summary> Executes and observes a Unity Editor Play Mode enter transition. </summary>
    internal sealed class PlayEnterTransitionRunner
    {
        private const int RejectedStoppedObservationThreshold = 2;

        private readonly IServerVersionProvider serverVersionProvider;
        private readonly IUnityEditorReadinessGate readinessGate;
        private readonly UnityProjectIdentity projectIdentity;
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
            UnityProjectIdentity projectIdentity,
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
        /// <param name="executionContext"> The action-owned durable context used to resume after domain reload. </param>
        /// <param name="executionDeadlineCancellationToken"> Cancellation controlled only by the immutable execution deadline. </param>
        /// <returns> The structured transition result. </returns>
        public async Task<PlayEnterTransitionExecutionResult> EnterAsync (
            IPlayEnterLifecycleExecutionContext? executionContext,
            CancellationToken executionDeadlineCancellationToken)
        {
            executionDeadlineCancellationToken.ThrowIfCancellationRequested();
            var before = CaptureObservation();

            if (executionContext != null && executionContext.HasSideEffectAdmission)
            {
                if (!executionContext.TryReadBefore(
                        out var pendingBefore,
                        out var pendingReadErrorMessage))
                {
                    return CreateFailure(
                        PlayModeErrorCodes.PlayModeStateUnknown,
                        $"Recoverable Play Mode enter state is invalid. {pendingReadErrorMessage}",
                        before,
                        before,
                        ExecutionApplicationState.Unknown);
                }

                return await ResumePendingEnterAsync(
                    pendingBefore,
                    before,
                    executionDeadlineCancellationToken);
            }

            var preconditionFailure = ValidatePreconditions(before);
            if (preconditionFailure != null)
            {
                return preconditionFailure;
            }

            if (IsEnteredSnapshot(before))
            {
                return CreateSuccess(PlayLifecycleTransitionOutcome.AlreadyEntered, before, before);
            }

            // NOTE: This must be persisted before Unity is asked to enter Play Mode.
            // Entering Play Mode can trigger domain reload before this daemon can respond.
            var ownsSideEffectAdmission = await TryPersistPendingEnterAsync(
                executionContext,
                before,
                executionDeadlineCancellationToken);
            if (!ownsSideEffectAdmission)
            {
                if (!executionContext!.TryReadBefore(
                        out var pendingBefore,
                        out var pendingReadErrorMessage))
                {
                    return CreateFailure(
                        PlayModeErrorCodes.PlayModeStateUnknown,
                        $"Recoverable Play Mode enter state is invalid. {pendingReadErrorMessage}",
                        before,
                        before,
                        ExecutionApplicationState.Unknown);
                }

                return await ResumePendingEnterAsync(
                    pendingBefore,
                    CaptureObservation(),
                    executionDeadlineCancellationToken);
            }

            var mutationActivity = mutationLaneControl.BeginMutation();
            try
            {
                executionDeadlineCancellationToken
                    .ThrowIfCancellationRequested();
                playModeController.EnterPlayMode();
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
                    PlayModeErrorCodes.PlayModeEnterRejected,
                    $"Unity rejected Play Mode enter. {exception.Message}",
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
                var result = await ObserveRequestedEnterAsync(
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

        private async Task<PlayEnterTransitionExecutionResult> ResumePendingEnterAsync (
            UnityEditorObservation pendingBefore,
            UnityEditorObservation current,
            CancellationToken executionDeadlineCancellationToken)
        {
            if (IsRecoverablePendingEnter(pendingBefore, current))
            {
                return CreateSuccess(PlayLifecycleTransitionOutcome.Entered, pendingBefore, current);
            }

            var mutationActivity = mutationLaneControl.BeginMutation();
            try
            {
                var result = await ObserveRequestedEnterAsync(
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
            UnityEditorObservation before,
            UnityEditorObservation initialObserved,
            bool classifyInitialObservation,
            CancellationToken executionDeadlineCancellationToken)
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
                    executionDeadlineCancellationToken.ThrowIfCancellationRequested();
                    await editorUpdateAwaiter.WaitForNextUpdateAsync(
                        executionDeadlineCancellationToken);
                    observed = CaptureObservation();

                    if (IsEnteredSnapshot(observed) && HasGenerationChanged(before, observed))
                    {
                        return CreateSuccess(PlayLifecycleTransitionOutcome.Entered, before, observed);
                    }

                    var observedFailure = ClassifyObservedFailure(before, observed, ref stoppedObservations);
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

        private static async Task<bool> TryPersistPendingEnterAsync (
            IPlayEnterLifecycleExecutionContext? executionContext,
            UnityEditorObservation before,
            CancellationToken cancellationToken)
        {
            if (executionContext == null)
            {
                return true;
            }

            return await executionContext.TryAdmitSideEffectAsync(
                before,
                cancellationToken);
        }

        private PlayEnterTransitionExecutionResult ValidatePreconditions (UnityEditorObservation before)
        {
            if (before.State.EditorMode != UnityEditorMode.Gui)
            {
                return CreateFailure(
                    PlayModeErrorCodes.PlayModeRequiresGuiEditor,
                    "Play Mode enter requires a GUI Editor session.",
                    before,
                    before,
                    ExecutionApplicationState.NotApplied);
            }

            if (before.State.PlayMode == null || IsUnknownPlayMode(before))
            {
                return CreateFailure(
                    PlayModeErrorCodes.PlayModeStateUnknown,
                    "Unity Play Mode state is unknown before entering Play Mode.",
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
                    ExecutionApplicationState.NotApplied);
            }

            return null;
        }

        private PlayEnterTransitionExecutionResult ClassifyObservedFailure (
            UnityEditorObservation before,
            UnityEditorObservation observed,
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
                    ExecutionApplicationState.Unknown);
            }

            TryReadPlayModeSnapshot(
                observed,
                out _,
                out var observedPlayModeState,
                out var observedPlayModeTransition);
            if (observedPlayModeState == UnityEditorPlayModeState.Entering
                || observedPlayModeTransition == UnityEditorPlayModeTransition.Entering)
            {
                stoppedObservations = 0;
                return null;
            }

            if (observedPlayModeState == UnityEditorPlayModeState.Exiting
                || observedPlayModeTransition == UnityEditorPlayModeTransition.Exiting)
            {
                return CreateFailure(
                    PlayModeErrorCodes.PlayModeAlreadyChanging,
                    "Unity Play Mode started exiting while enter was requested.",
                    before,
                    observed,
                    ExecutionApplicationState.Unknown);
            }

            if (!IsReadyOrPlayModeLifecycle(observed))
            {
                return CreateFailure(
                    PlayModeErrorCodes.PlayModeTransitionBlocked,
                    $"Unity Play Mode enter was blocked by lifecycleState={TextVocabulary.GetText(observed.State.LifecycleState)}.",
                    before,
                    observed,
                    ExecutionApplicationState.Unknown);
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
                        ExecutionApplicationState.NotApplied);
                }
            }
            else
            {
                stoppedObservations = 0;
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

        private static PlayEnterTransitionExecutionResult CreateSuccess (
            PlayLifecycleTransitionOutcome result,
            UnityEditorObservation before,
            UnityEditorObservation after)
        {
            return PlayEnterTransitionExecutionResult.Success(
                new PlayEnterTransitionExecutionResponse(
                    new PlayLifecycleTransitionResult(
                    Transition: PlayLifecycleTransitionCommand.Enter,
                    Result: result,
                    Before: before,
                    After: after,
                    Observed: null,
                    ApplicationState: null)));
        }

        private static PlayEnterTransitionExecutionResult CreateFailure (
            UcliCode code,
            string message,
            UnityEditorObservation before,
            UnityEditorObservation observed,
            ExecutionApplicationState applicationState)
        {
            var response = new PlayEnterTransitionExecutionResponse(
                new PlayLifecycleTransitionResult(
                    Transition: PlayLifecycleTransitionCommand.Enter,
                    Result: PlayLifecycleTransitionOutcome.Blocked,
                    Before: before,
                    After: null,
                    Observed: observed,
                    ApplicationState: applicationState));
            return PlayEnterTransitionExecutionResult.Failure(
                response,
                new PlayTransitionExecutionError(code, message));
        }

        private static PlayEnterTransitionExecutionResult CreateTimeout (
            UnityEditorObservation before,
            UnityEditorObservation observed)
        {
            var response = new PlayEnterTransitionExecutionResponse(
                new PlayLifecycleTransitionResult(
                    Transition: PlayLifecycleTransitionCommand.Enter,
                    Result: PlayLifecycleTransitionOutcome.Timeout,
                    Before: before,
                    After: null,
                    Observed: observed,
                    ApplicationState: ExecutionApplicationState.Indeterminate));
            return PlayEnterTransitionExecutionResult.Failure(
                response,
                new PlayTransitionExecutionError(
                    PlayModeErrorCodes.PlayModeTransitionTimeout,
                    "Unity Play Mode enter reached its execution deadline."));
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
            return TryReadPlayModeSnapshot(
                    snapshot,
                    out var playMode,
                    out var playModeState,
                    out var playModeTransition)
                && snapshot.State.LifecycleState == UnityEditorLifecycleState.Ready
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

        private static bool IsReadyOrPlayModeLifecycle (UnityEditorObservation snapshot)
        {
            return snapshot.State.LifecycleState is UnityEditorLifecycleState.Ready or UnityEditorLifecycleState.PlayMode;
        }

        private static bool IsEnterTransitionLifecycle (UnityEditorLifecycleState lifecycleState)
        {
            return lifecycleState is UnityEditorLifecycleState.Starting
                or UnityEditorLifecycleState.Recovering
                or UnityEditorLifecycleState.Compiling
                or UnityEditorLifecycleState.DomainReloading
                or UnityEditorLifecycleState.Reimporting;
        }

        private static bool IsRecoverablePendingEnter (
            UnityEditorObservation pendingBefore,
            UnityEditorObservation current)
        {
            return pendingBefore != null
                && current != null
                && IsReadyStoppedSnapshot(pendingBefore)
                && IsEnteredSnapshot(current)
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
