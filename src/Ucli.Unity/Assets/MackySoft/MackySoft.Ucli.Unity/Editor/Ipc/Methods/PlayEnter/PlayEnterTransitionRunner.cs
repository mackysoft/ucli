using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using UnityEditor;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Executes and observes a Unity Editor Play Mode enter transition. </summary>
    internal sealed class PlayEnterTransitionRunner
    {
        private const int RejectedStoppedObservationThreshold = 2;

        private readonly IServerVersionProvider serverVersionProvider;
        private readonly IUnityEditorReadinessGate readinessGate;
        private readonly IpcProjectIdentity projectIdentity;
        private readonly Func<CancellationToken, Task> editorUpdateAwaiter;
        private readonly Action enterPlayModeRequester;
        private readonly IDaemonLogger daemonLogger;

        /// <summary> Initializes a new instance of the <see cref="PlayEnterTransitionRunner" /> class. </summary>
        /// <param name="serverVersionProvider"> The server-version provider dependency. </param>
        /// <param name="readinessGate"> The lifecycle snapshot provider dependency. </param>
        /// <param name="projectIdentity"> The project identity served by this IPC host. </param>
        /// <param name="daemonLogger"> The daemon logger dependency. </param>
        public PlayEnterTransitionRunner (
            IServerVersionProvider serverVersionProvider,
            IUnityEditorReadinessGate readinessGate,
            IpcProjectIdentity projectIdentity,
            IDaemonLogger daemonLogger = null)
            : this(
                serverVersionProvider,
                readinessGate,
                projectIdentity,
                UnityEditorUpdateAwaiter.WaitForNextUpdateAsync,
                static () => EditorApplication.EnterPlaymode(),
                daemonLogger)
        {
        }

        /// <summary> Initializes a new instance of the <see cref="PlayEnterTransitionRunner" /> class. </summary>
        /// <param name="serverVersionProvider"> The server-version provider dependency. </param>
        /// <param name="readinessGate"> The lifecycle snapshot provider dependency. </param>
        /// <param name="projectIdentity"> The project identity served by this IPC host. </param>
        /// <param name="editorUpdateAwaiter"> The editor update awaiter dependency. </param>
        /// <param name="enterPlayModeRequester"> The Unity Play Mode enter requester dependency. </param>
        /// <param name="daemonLogger"> The daemon logger dependency. </param>
        internal PlayEnterTransitionRunner (
            IServerVersionProvider serverVersionProvider,
            IUnityEditorReadinessGate readinessGate,
            IpcProjectIdentity projectIdentity,
            Func<CancellationToken, Task> editorUpdateAwaiter,
            Action enterPlayModeRequester,
            IDaemonLogger daemonLogger = null)
        {
            this.serverVersionProvider = serverVersionProvider ?? throw new ArgumentNullException(nameof(serverVersionProvider));
            this.readinessGate = readinessGate ?? throw new ArgumentNullException(nameof(readinessGate));
            this.projectIdentity = projectIdentity ?? throw new ArgumentNullException(nameof(projectIdentity));
            this.editorUpdateAwaiter = editorUpdateAwaiter ?? throw new ArgumentNullException(nameof(editorUpdateAwaiter));
            this.enterPlayModeRequester = enterPlayModeRequester ?? throw new ArgumentNullException(nameof(enterPlayModeRequester));
            this.daemonLogger = daemonLogger ?? NoOpDaemonLogger.Instance;
        }

        /// <summary> Executes Play Mode enter and waits until Unity reports an entered snapshot. </summary>
        /// <param name="timeoutMilliseconds"> The transition timeout in milliseconds. </param>
        /// <param name="recoverableContext"> The persisted operation context used to resume after domain reload. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by the IPC request. </param>
        /// <returns> The structured transition result. </returns>
        public async Task<PlayEnterTransitionExecutionResult> EnterAsync (
            int timeoutMilliseconds,
            RecoverableIpcOperationContext recoverableContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var before = CaptureSnapshot();

            if (recoverableContext != null && recoverableContext.HasOperationRecord)
            {
                if (!TryReadPendingEnter(recoverableContext, out var pendingBefore, out var pendingReadErrorMessage))
                {
                    return CreateFailure(
                        PlayModeErrorCodes.PlayModeStateUnknown,
                        $"Recoverable Play Mode enter state is invalid. {pendingReadErrorMessage}",
                        before,
                        before,
                        IpcPlayApplicationStateNames.Unknown);
                }

                return await ResumePendingEnterAsync(
                    pendingBefore,
                    before,
                    recoverableContext,
                    timeoutMilliseconds,
                    cancellationToken);
            }

            var preconditionFailure = ValidatePreconditions(before);
            if (preconditionFailure != null)
            {
                return preconditionFailure;
            }

            if (IsEnteredSnapshot(before))
            {
                return CreateSuccess(IpcPlayTransitionResultNames.AlreadyEntered, before, before);
            }

            // NOTE: This must be persisted before EditorApplication.EnterPlaymode is called.
            // Entering Play Mode can trigger domain reload before this daemon can respond.
            var persistFailure = TryPersistPendingEnter(recoverableContext, before);
            if (persistFailure != null)
            {
                return persistFailure;
            }

            try
            {
                enterPlayModeRequester();
            }
            catch (Exception exception)
            {
                return CreateFailure(
                    PlayModeErrorCodes.PlayModeEnterRejected,
                    $"Unity rejected Play Mode enter. {exception.Message}",
                    before,
                    before,
                    IpcPlayApplicationStateNames.NotApplied);
            }

            return await ObserveRequestedEnterAsync(
                before,
                before,
                TimeSpan.FromMilliseconds(timeoutMilliseconds),
                timeoutMilliseconds,
                classifyInitialObservation: false,
                cancellationToken);
        }

        private async Task<PlayEnterTransitionExecutionResult> ResumePendingEnterAsync (
            IpcPlayLifecycleSnapshot pendingBefore,
            IpcPlayLifecycleSnapshot current,
            RecoverableIpcOperationContext recoverableContext,
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            if (IsRecoverablePendingEnter(pendingBefore, current))
            {
                return CreateSuccess(IpcPlayTransitionResultNames.Entered, pendingBefore, current);
            }

            var remainingTimeout = ResolveRemainingPendingTimeout(
                recoverableContext,
                timeoutMilliseconds);
            if (remainingTimeout <= TimeSpan.Zero)
            {
                return CreateTimeout(pendingBefore, current, timeoutMilliseconds);
            }

            return await ObserveRequestedEnterAsync(
                pendingBefore,
                current,
                remainingTimeout,
                timeoutMilliseconds,
                classifyInitialObservation: true,
                cancellationToken);
        }

        private async Task<PlayEnterTransitionExecutionResult> ObserveRequestedEnterAsync (
            IpcPlayLifecycleSnapshot before,
            IpcPlayLifecycleSnapshot initialObserved,
            TimeSpan remainingTimeout,
            int timeoutMilliseconds,
            bool classifyInitialObservation,
            CancellationToken cancellationToken)
        {
            var observed = initialObserved;
            var stoppedObservations = 0;
            using var timeoutCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCancellationTokenSource.CancelAfter(remainingTimeout);
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
                    timeoutCancellationTokenSource.Token.ThrowIfCancellationRequested();
                    await editorUpdateAwaiter(timeoutCancellationTokenSource.Token);
                    observed = CaptureSnapshot();

                    if (IsEnteredSnapshot(observed) && HasGenerationChanged(before, observed))
                    {
                        return CreateSuccess(IpcPlayTransitionResultNames.Entered, before, observed);
                    }

                    var observedFailure = ClassifyObservedFailure(before, observed, ref stoppedObservations);
                    if (observedFailure != null)
                    {
                        return observedFailure;
                    }
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return CreateTimeout(before, observed, timeoutMilliseconds);
            }
        }

        private bool TryReadPendingEnter (
            RecoverableIpcOperationContext recoverableContext,
            out IpcPlayLifecycleSnapshot before,
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

        private PlayEnterTransitionExecutionResult TryPersistPendingEnter (
            RecoverableIpcOperationContext recoverableContext,
            IpcPlayLifecycleSnapshot before)
        {
            if (recoverableContext == null)
            {
                return null;
            }

            return recoverableContext.TryMarkPending(new PlayEnterRecoveryPayload(before), out var errorMessage)
                ? null
                : CreateFailure(
                    PlayModeErrorCodes.PlayModeEnterRejected,
                    $"Unity Play Mode enter could not persist transition recovery state. {errorMessage}",
                    before,
                    before,
                    IpcPlayApplicationStateNames.NotApplied);
        }

        private static TimeSpan ResolveRemainingPendingTimeout (
            RecoverableIpcOperationContext recoverableContext,
            int timeoutMilliseconds)
        {
            var totalTimeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);
            if (recoverableContext?.StartedAtUtc == null)
            {
                return totalTimeout;
            }

            var elapsed = DateTimeOffset.UtcNow - recoverableContext.StartedAtUtc.Value;
            return elapsed >= totalTimeout
                ? TimeSpan.Zero
                : totalTimeout - elapsed;
        }

        private PlayEnterTransitionExecutionResult ValidatePreconditions (IpcPlayLifecycleSnapshot before)
        {
            if (!string.Equals(before.EditorMode, DaemonEditorModeValues.Gui, StringComparison.Ordinal))
            {
                return CreateFailure(
                    PlayModeErrorCodes.PlayModeRequiresGuiEditor,
                    "Play Mode enter requires a GUI Editor session.",
                    before,
                    before,
                    IpcPlayApplicationStateNames.NotApplied);
            }

            if (before.PlayMode == null || IsUnknownPlayMode(before))
            {
                return CreateFailure(
                    PlayModeErrorCodes.PlayModeStateUnknown,
                    "Unity Play Mode state is unknown before entering Play Mode.",
                    before,
                    before,
                    IpcPlayApplicationStateNames.Unknown);
            }

            if (IsPlayModeChanging(before))
            {
                return CreateFailure(
                    PlayModeErrorCodes.PlayModeAlreadyChanging,
                    "Unity Play Mode is already changing.",
                    before,
                    before,
                    IpcPlayApplicationStateNames.NotApplied);
            }

            if (IsEnteredSnapshot(before))
            {
                return null;
            }

            if (!IsReadyStoppedSnapshot(before))
            {
                return CreateFailure(
                    PlayModeErrorCodes.PlayModeTransitionBlocked,
                    $"Unity Play Mode enter is blocked by lifecycleState={before.LifecycleState ?? "null"}.",
                    before,
                    before,
                    IpcPlayApplicationStateNames.NotApplied);
            }

            return null;
        }

        private PlayEnterTransitionExecutionResult ClassifyObservedFailure (
            IpcPlayLifecycleSnapshot before,
            IpcPlayLifecycleSnapshot observed,
            ref int stoppedObservations)
        {
            if (observed.PlayMode == null || IsUnknownPlayMode(observed))
            {
                return CreateFailure(
                    PlayModeErrorCodes.PlayModeStateUnknown,
                    "Unity Play Mode state became unknown while entering Play Mode.",
                    before,
                    observed,
                    IpcPlayApplicationStateNames.Unknown);
            }

            if (string.Equals(observed.PlayMode.State, IpcPlayModeStateNames.Entering, StringComparison.Ordinal)
                || string.Equals(observed.PlayMode.Transition, IpcPlayModeTransitionNames.Entering, StringComparison.Ordinal))
            {
                stoppedObservations = 0;
                return null;
            }

            if (string.Equals(observed.PlayMode.State, IpcPlayModeStateNames.Exiting, StringComparison.Ordinal)
                || string.Equals(observed.PlayMode.Transition, IpcPlayModeTransitionNames.Exiting, StringComparison.Ordinal))
            {
                return CreateFailure(
                    PlayModeErrorCodes.PlayModeAlreadyChanging,
                    "Unity Play Mode started exiting while enter was requested.",
                    before,
                    observed,
                    IpcPlayApplicationStateNames.Unknown);
            }

            if (!IsReadyOrPlayModeLifecycle(observed))
            {
                return CreateFailure(
                    PlayModeErrorCodes.PlayModeTransitionBlocked,
                    $"Unity Play Mode enter was blocked by lifecycleState={observed.LifecycleState ?? "null"}.",
                    before,
                    observed,
                    IpcPlayApplicationStateNames.Unknown);
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
                        IpcPlayApplicationStateNames.NotApplied);
                }
            }
            else
            {
                stoppedObservations = 0;
            }

            return null;
        }

        private IpcPlayLifecycleSnapshot CaptureSnapshot ()
        {
            return UnityLifecycleResponseCodec.CreatePlayLifecycleSnapshot(
                projectIdentity.UnityVersion,
                serverVersionProvider.GetVersion(),
                projectIdentity.ProjectFingerprint,
                readinessGate.CaptureSnapshot());
        }

        private static PlayEnterTransitionExecutionResult CreateSuccess (
            string result,
            IpcPlayLifecycleSnapshot before,
            IpcPlayLifecycleSnapshot after)
        {
            return PlayEnterTransitionExecutionResult.Success(new IpcPlayTransitionResponse(
                new IpcPlayTransitionResult(
                    Transition: IpcPlayTransitionCommandNames.Enter,
                    Result: result,
                    Before: before)
                {
                    After = after,
                }));
        }

        private static PlayEnterTransitionExecutionResult CreateFailure (
            UcliCode code,
            string message,
            IpcPlayLifecycleSnapshot before,
            IpcPlayLifecycleSnapshot observed,
            string applicationState)
        {
            var response = new IpcPlayTransitionResponse(
                new IpcPlayTransitionResult(
                    Transition: IpcPlayTransitionCommandNames.Enter,
                    Result: IpcPlayTransitionResultNames.Blocked,
                    Before: before)
                {
                    Observed = observed,
                    ApplicationState = applicationState,
                });
            return PlayEnterTransitionExecutionResult.Failure(response, new IpcError(code, message, null));
        }

        private static PlayEnterTransitionExecutionResult CreateTimeout (
            IpcPlayLifecycleSnapshot before,
            IpcPlayLifecycleSnapshot observed,
            int timeoutMilliseconds)
        {
            var response = new IpcPlayTransitionResponse(
                new IpcPlayTransitionResult(
                    Transition: IpcPlayTransitionCommandNames.Enter,
                    Result: IpcPlayTransitionResultNames.Timeout,
                    Before: before)
                {
                    Observed = observed,
                    ApplicationState = IpcPlayApplicationStateNames.Indeterminate,
                });
            return PlayEnterTransitionExecutionResult.Failure(
                response,
                new IpcError(
                    PlayModeErrorCodes.PlayModeTransitionTimeout,
                    $"Unity Play Mode enter timed out after {timeoutMilliseconds} milliseconds.",
                    null));
        }

        private static bool IsEnteredSnapshot (IpcPlayLifecycleSnapshot snapshot)
        {
            var playMode = snapshot.PlayMode;
            return playMode != null
                && string.Equals(snapshot.LifecycleState, IpcEditorLifecycleStateCodec.Playmode, StringComparison.Ordinal)
                && string.Equals(playMode.State, IpcPlayModeStateNames.Playing, StringComparison.Ordinal)
                && string.Equals(playMode.Transition, IpcPlayModeTransitionNames.None, StringComparison.Ordinal)
                && playMode.IsPlaying
                && !snapshot.CanAcceptExecutionRequests;
        }

        private static bool HasGenerationChanged (
            IpcPlayLifecycleSnapshot before,
            IpcPlayLifecycleSnapshot after)
        {
            return !string.Equals(before.PlayMode?.Generation, after.PlayMode?.Generation, StringComparison.Ordinal);
        }

        private static bool IsReadyStoppedSnapshot (IpcPlayLifecycleSnapshot snapshot)
        {
            var playMode = snapshot.PlayMode;
            return playMode != null
                && string.Equals(snapshot.LifecycleState, IpcEditorLifecycleStateCodec.Ready, StringComparison.Ordinal)
                && string.Equals(playMode.State, IpcPlayModeStateNames.Stopped, StringComparison.Ordinal)
                && string.Equals(playMode.Transition, IpcPlayModeTransitionNames.None, StringComparison.Ordinal)
                && !playMode.IsPlaying
                && !playMode.IsPlayingOrWillChangePlaymode;
        }

        private static bool IsPlayModeChanging (IpcPlayLifecycleSnapshot snapshot)
        {
            var playMode = snapshot.PlayMode;
            return playMode != null
                && (string.Equals(playMode.State, IpcPlayModeStateNames.Entering, StringComparison.Ordinal)
                    || string.Equals(playMode.State, IpcPlayModeStateNames.Exiting, StringComparison.Ordinal)
                    || string.Equals(playMode.Transition, IpcPlayModeTransitionNames.Entering, StringComparison.Ordinal)
                    || string.Equals(playMode.Transition, IpcPlayModeTransitionNames.Exiting, StringComparison.Ordinal));
        }

        private static bool IsUnknownPlayMode (IpcPlayLifecycleSnapshot snapshot)
        {
            return string.Equals(snapshot.PlayMode?.State, IpcPlayModeStateNames.Unknown, StringComparison.Ordinal);
        }

        private static bool IsReadyOrPlayModeLifecycle (IpcPlayLifecycleSnapshot snapshot)
        {
            return string.Equals(snapshot.LifecycleState, IpcEditorLifecycleStateCodec.Ready, StringComparison.Ordinal)
                || string.Equals(snapshot.LifecycleState, IpcEditorLifecycleStateCodec.Playmode, StringComparison.Ordinal);
        }

        private static bool IsRecoverablePendingEnter (
            IpcPlayLifecycleSnapshot pendingBefore,
            IpcPlayLifecycleSnapshot current)
        {
            return pendingBefore != null
                && current != null
                && IsReadyStoppedSnapshot(pendingBefore)
                && IsEnteredSnapshot(current)
                && string.Equals(pendingBefore.ProjectFingerprint, current.ProjectFingerprint, StringComparison.Ordinal)
                && HasGenerationChanged(pendingBefore, current);
        }

    }
}
