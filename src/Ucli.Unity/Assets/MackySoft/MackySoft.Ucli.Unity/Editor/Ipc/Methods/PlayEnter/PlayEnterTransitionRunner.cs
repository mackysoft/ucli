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
        private readonly IPlayEnterPendingTransitionStore pendingTransitionStore;
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
                WaitForNextEditorUpdateAsync,
                static () => EditorApplication.isPlaying = true,
                FilePlayEnterPendingTransitionStore.Create(projectIdentity),
                daemonLogger)
        {
        }

        /// <summary> Initializes a new instance of the <see cref="PlayEnterTransitionRunner" /> class. </summary>
        /// <param name="serverVersionProvider"> The server-version provider dependency. </param>
        /// <param name="readinessGate"> The lifecycle snapshot provider dependency. </param>
        /// <param name="projectIdentity"> The project identity served by this IPC host. </param>
        /// <param name="editorUpdateAwaiter"> The editor update awaiter dependency. </param>
        /// <param name="enterPlayModeRequester"> The Unity Play Mode enter requester dependency. </param>
        /// <param name="pendingTransitionStore"> Optional pending-transition store used by tests and recovery. </param>
        /// <param name="daemonLogger"> The daemon logger dependency. </param>
        internal PlayEnterTransitionRunner (
            IServerVersionProvider serverVersionProvider,
            IUnityEditorReadinessGate readinessGate,
            IpcProjectIdentity projectIdentity,
            Func<CancellationToken, Task> editorUpdateAwaiter,
            Action enterPlayModeRequester,
            IPlayEnterPendingTransitionStore pendingTransitionStore = null,
            IDaemonLogger daemonLogger = null)
        {
            this.serverVersionProvider = serverVersionProvider ?? throw new ArgumentNullException(nameof(serverVersionProvider));
            this.readinessGate = readinessGate ?? throw new ArgumentNullException(nameof(readinessGate));
            this.projectIdentity = projectIdentity ?? throw new ArgumentNullException(nameof(projectIdentity));
            this.editorUpdateAwaiter = editorUpdateAwaiter ?? throw new ArgumentNullException(nameof(editorUpdateAwaiter));
            this.enterPlayModeRequester = enterPlayModeRequester ?? throw new ArgumentNullException(nameof(enterPlayModeRequester));
            this.pendingTransitionStore = pendingTransitionStore;
            this.daemonLogger = daemonLogger ?? NoOpDaemonLogger.Instance;
        }

        /// <summary> Executes Play Mode enter and waits until Unity reports an entered snapshot. </summary>
        /// <param name="timeoutMilliseconds"> The transition timeout in milliseconds. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by the IPC request. </param>
        /// <returns> The structured transition result. </returns>
        public async Task<PlayEnterTransitionExecutionResult> EnterAsync (
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var before = CaptureSnapshot();

            var preconditionFailure = ValidatePreconditions(before);
            if (preconditionFailure != null)
            {
                return preconditionFailure;
            }

            if (IsEnteredSnapshot(before))
            {
                var pendingCompletion = TryCompletePendingEnter(before);
                if (pendingCompletion != null)
                {
                    return pendingCompletion;
                }

                return CreateSuccess(IpcPlayTransitionResultNames.AlreadyEntered, before, before);
            }

            var persistFailure = TryPersistPendingEnter(before);
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
                DeletePendingEnter();
                return CreateFailure(
                    PlayModeErrorCodes.PlayModeEnterRejected,
                    $"Unity rejected Play Mode enter. {exception.Message}",
                    before,
                    before,
                    IpcPlayApplicationStateNames.NotApplied);
            }

            var observed = before;
            var stoppedObservations = 0;
            using var timeoutCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCancellationTokenSource.CancelAfter(timeoutMilliseconds);
            try
            {
                while (true)
                {
                    timeoutCancellationTokenSource.Token.ThrowIfCancellationRequested();
                    await editorUpdateAwaiter(timeoutCancellationTokenSource.Token);
                    observed = CaptureSnapshot();

                    if (IsEnteredSnapshot(observed) && HasGenerationChanged(before, observed))
                    {
                        DeletePendingEnter();
                        return CreateSuccess(IpcPlayTransitionResultNames.Entered, before, observed);
                    }

                    var observedFailure = ClassifyObservedFailure(before, observed, ref stoppedObservations);
                    if (observedFailure != null)
                    {
                        DeletePendingEnter();
                        return observedFailure;
                    }
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                DeletePendingEnter();
                return CreateTimeout(before, observed, timeoutMilliseconds);
            }
        }

        private PlayEnterTransitionExecutionResult TryCompletePendingEnter (IpcPlayLifecycleSnapshot current)
        {
            if (pendingTransitionStore == null)
            {
                return null;
            }

            if (!pendingTransitionStore.TryRead(out var pendingBefore, out var errorMessage))
            {
                if (!string.IsNullOrWhiteSpace(errorMessage))
                {
                    daemonLogger.Warning(
                        DaemonLogCategories.Lifecycle,
                        $"Play Mode enter pending transition read failed. {errorMessage}");
                }

                return null;
            }

            if (!IsRecoverablePendingEnter(pendingBefore, current))
            {
                DeletePendingEnter();
                return null;
            }

            DeletePendingEnter();
            return CreateSuccess(IpcPlayTransitionResultNames.Entered, pendingBefore, current);
        }

        private PlayEnterTransitionExecutionResult TryPersistPendingEnter (IpcPlayLifecycleSnapshot before)
        {
            if (pendingTransitionStore == null)
            {
                return null;
            }

            return pendingTransitionStore.TryWrite(before, out var errorMessage)
                ? null
                : CreateFailure(
                    PlayModeErrorCodes.PlayModeEnterRejected,
                    $"Unity Play Mode enter could not persist transition recovery state. {errorMessage}",
                    before,
                    before,
                    IpcPlayApplicationStateNames.NotApplied);
        }

        private void DeletePendingEnter ()
        {
            if (pendingTransitionStore == null)
            {
                return;
            }

            if (!pendingTransitionStore.TryDelete(out var errorMessage))
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Lifecycle,
                    $"Play Mode enter pending transition cleanup failed. {errorMessage}");
            }
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

        private static Task WaitForNextEditorUpdateAsync (CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new EditorUpdateWaitState(cancellationToken).Attach();
        }

        private sealed class EditorUpdateWaitState
        {
            private readonly CancellationToken cancellationToken;

            private readonly SynchronizationContext synchronizationContext;

            private readonly TaskCompletionSource<object> completionSource =
                new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            private CancellationTokenRegistration cancellationRegistration;

            private int detached;

            public EditorUpdateWaitState (CancellationToken cancellationToken)
            {
                this.cancellationToken = cancellationToken;
                synchronizationContext = SynchronizationContext.Current;
            }

            public Task Attach ()
            {
                EditorApplication.update += CompleteOnEditorUpdate;
                if (cancellationToken.CanBeCanceled)
                {
                    cancellationRegistration = cancellationToken.Register(static state =>
                    {
                        var waitState = (EditorUpdateWaitState)state;
                        waitState.Cancel();
                    }, this);
                    if (Volatile.Read(ref detached) != 0)
                    {
                        cancellationRegistration.Dispose();
                    }
                }

                return completionSource.Task;
            }

            private void CompleteOnEditorUpdate ()
            {
                DetachOnMainThread();
                completionSource.TrySetResult(null);
            }

            private void Cancel ()
            {
                completionSource.TrySetCanceled(cancellationToken);
                if (synchronizationContext == null)
                {
                    return;
                }

                if (SynchronizationContext.Current == synchronizationContext)
                {
                    DetachOnMainThread();
                    return;
                }

                synchronizationContext.Post(static state =>
                {
                    var waitState = (EditorUpdateWaitState)state;
                    waitState.DetachOnMainThread();
                }, this);
            }

            private void DetachOnMainThread ()
            {
                if (Interlocked.Exchange(ref detached, 1) != 0)
                {
                    return;
                }

                EditorApplication.update -= CompleteOnEditorUpdate;
                cancellationRegistration.Dispose();
            }
        }
    }
}
