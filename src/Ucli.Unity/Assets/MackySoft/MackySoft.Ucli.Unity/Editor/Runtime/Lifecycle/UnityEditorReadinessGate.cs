using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using UnityEditor;
using UnityEditor.Compilation;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Captures Unity editor lifecycle telemetry and gates execution requests. </summary>
    internal sealed class UnityEditorReadinessGate :
        IUnityEditorReadinessGate,
        IUnityEditorAvailabilityObservationSource
    {
        private static readonly UnityEditorLifecycleTelemetryState SharedLifecycleTelemetryState = new UnityEditorLifecycleTelemetryState();

        private static readonly UnityEditorLifecycleMonitor SharedLifecycleMonitor = new UnityEditorLifecycleMonitor(
            SharedLifecycleTelemetryState,
            static () => EditorApplication.isCompiling,
            static () => EditorApplication.isUpdating,
            static () => EditorApplication.isPlaying,
            static () => EditorApplication.isPlayingOrWillChangePlaymode);

        private readonly UnityEditorLifecycleMonitor lifecycleMonitor;

        private readonly Func<bool> isPlayModeMutationActiveProvider;

        private readonly IUnityMutationExecutionState mutationExecutionState;

        private readonly DaemonEditorMode editorMode;

        private readonly Action<AssemblyReloadEvents.AssemblyReloadCallback> beforeAssemblyReloadSubscriber;

        private readonly Action<AssemblyReloadEvents.AssemblyReloadCallback> beforeAssemblyReloadUnsubscriber;

        private readonly Action<EditorApplication.CallbackFunction> editorUpdateSubscriber;

        private readonly Action<EditorApplication.CallbackFunction> editorUpdateUnsubscriber;

        private readonly Action<Action> quittingSubscriber;

        private readonly Action<Action> quittingUnsubscriber;

        /// <summary> Gets a value indicating whether the editor is ready to start IPC host bootstrap. </summary>
        internal static bool IsReadyForBootstrapStartup => !EditorApplication.isCompiling;

        /// <summary> Initializes a new instance of the <see cref="UnityEditorReadinessGate" /> class. </summary>
        /// <param name="editorMode"> The daemon Editor mode reported by Unity Editor observations. </param>
        /// <param name="mutationExecutionState"> The exclusive mutation-lane state exposed to lifecycle telemetry. </param>
        public UnityEditorReadinessGate (
            DaemonEditorMode editorMode,
            IUnityMutationExecutionState mutationExecutionState)
            : this(
                editorMode,
                SharedLifecycleMonitor,
                static () => EditorApplication.isPlaying,
                mutationExecutionState,
                static handler => AssemblyReloadEvents.beforeAssemblyReload += handler,
                static handler => AssemblyReloadEvents.beforeAssemblyReload -= handler,
                static handler => EditorApplication.update += handler,
                static handler => EditorApplication.update -= handler,
                static handler => EditorApplication.quitting += handler,
                static handler => EditorApplication.quitting -= handler,
                subscribeToEditorEvents: true)
        {
        }

        /// <summary> Initializes a new instance of the <see cref="UnityEditorReadinessGate" /> class. </summary>
        /// <param name="editorMode"> The daemon Editor mode reported by Unity Editor observations. </param>
        /// <param name="lifecycleMonitor"> The lifecycle monitor dependency. </param>
        /// <param name="isPlayModeMutationActiveProvider"> The active Play Mode observer used by Play Mode mutation readiness. </param>
        /// <param name="mutationExecutionState"> The exclusive mutation-lane state exposed to lifecycle telemetry. </param>
        /// <param name="beforeAssemblyReloadSubscriber"> Subscribes one handler to the assembly-reload start event. </param>
        /// <param name="beforeAssemblyReloadUnsubscriber"> Unsubscribes one handler from the assembly-reload start event. </param>
        /// <param name="editorUpdateSubscriber"> Subscribes one handler to the editor update event. </param>
        /// <param name="editorUpdateUnsubscriber"> Unsubscribes one handler from the editor update event. </param>
        /// <param name="quittingSubscriber"> Subscribes one handler to the editor-quitting event. </param>
        /// <param name="quittingUnsubscriber"> Unsubscribes one handler from the editor-quitting event. </param>
        /// <param name="subscribeToEditorEvents"> Whether this instance should subscribe shared Unity editor lifecycle callbacks. </param>
        internal UnityEditorReadinessGate (
            DaemonEditorMode editorMode,
            UnityEditorLifecycleMonitor lifecycleMonitor,
            Func<bool> isPlayModeMutationActiveProvider,
            IUnityMutationExecutionState mutationExecutionState,
            Action<AssemblyReloadEvents.AssemblyReloadCallback> beforeAssemblyReloadSubscriber,
            Action<AssemblyReloadEvents.AssemblyReloadCallback> beforeAssemblyReloadUnsubscriber,
            Action<EditorApplication.CallbackFunction> editorUpdateSubscriber,
            Action<EditorApplication.CallbackFunction> editorUpdateUnsubscriber,
            Action<Action> quittingSubscriber,
            Action<Action> quittingUnsubscriber,
            bool subscribeToEditorEvents)
        {
            this.lifecycleMonitor = lifecycleMonitor ?? throw new ArgumentNullException(nameof(lifecycleMonitor));
            this.isPlayModeMutationActiveProvider = isPlayModeMutationActiveProvider ?? throw new ArgumentNullException(nameof(isPlayModeMutationActiveProvider));
            this.mutationExecutionState = mutationExecutionState ?? throw new ArgumentNullException(nameof(mutationExecutionState));
            _ = ContractLiteralCodec.ToValue(editorMode);
            this.editorMode = editorMode;
            this.beforeAssemblyReloadSubscriber = beforeAssemblyReloadSubscriber ?? throw new ArgumentNullException(nameof(beforeAssemblyReloadSubscriber));
            this.beforeAssemblyReloadUnsubscriber = beforeAssemblyReloadUnsubscriber ?? throw new ArgumentNullException(nameof(beforeAssemblyReloadUnsubscriber));
            this.editorUpdateSubscriber = editorUpdateSubscriber ?? throw new ArgumentNullException(nameof(editorUpdateSubscriber));
            this.editorUpdateUnsubscriber = editorUpdateUnsubscriber ?? throw new ArgumentNullException(nameof(editorUpdateUnsubscriber));
            this.quittingSubscriber = quittingSubscriber ?? throw new ArgumentNullException(nameof(quittingSubscriber));
            this.quittingUnsubscriber = quittingUnsubscriber ?? throw new ArgumentNullException(nameof(quittingUnsubscriber));
            if (!subscribeToEditorEvents)
            {
                return;
            }

            CompilationPipeline.compilationStarted -= OnCompilationStarted;
            CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompilationFinished;
            CompilationPipeline.compilationFinished -= OnCompilationFinished;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.wantsToQuit -= OnWantsToQuit;
            EditorApplication.quitting -= OnQuitting;

            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.wantsToQuit += OnWantsToQuit;
            EditorApplication.quitting += OnQuitting;
        }

        /// <inheritdoc />
        public UnityEditorObservation CaptureObservation ()
        {
            return CaptureEditorObservation();
        }

        /// <inheritdoc />
        public UnityEditorObservation CaptureAvailabilityObservation ()
        {
            var observation = CaptureEditorObservation();
            if (!observation.CanAcceptExecutionRequests || !mutationExecutionState.IsBusy)
            {
                return observation;
            }

            return observation.WithLifecycleState(IpcEditorLifecycleState.Busy);
        }

        /// <inheritdoc />
        public Task<UnityEditorExecutionReadinessResult> EnsureExecutionReadyAsync (
            bool failFast,
            CancellationToken cancellationToken = default,
            bool allowPlayMode = false)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var snapshot = CaptureEditorObservation();
            if (allowPlayMode)
            {
                return Task.FromResult(UnityEditorExecutionReadinessPolicy.CreatePlayModeAllowedResult(
                    snapshot,
                    isPlayModeMutationActiveProvider()));
            }

            if (snapshot.CanAcceptExecutionRequests)
            {
                return Task.FromResult(UnityEditorExecutionReadinessResult.Ready(snapshot));
            }

            if (failFast || !UnityEditorExecutionReadinessPolicy.IsWaitableState(snapshot.State.LifecycleState))
            {
                return Task.FromResult(UnityEditorExecutionReadinessPolicy.CreateBlockedResult(snapshot));
            }

            var waitState = new ReadinessWaitState(this, cancellationToken);
            return waitState.AttachAndWaitAsync();
        }

        /// <summary> Gets the current domain-reload generation used by plan-token environment snapshots. </summary>
        internal static long CurrentDomainReloadGeneration => SharedLifecycleTelemetryState.DomainReloadGeneration;

        /// <summary> Records completion of one asset refresh cycle. </summary>
        internal static void ObserveAssetRefreshCompleted ()
        {
            SharedLifecycleMonitor.OnAssetRefreshCompleted();
        }

        private UnityEditorObservation CaptureEditorObservation ()
        {
            return lifecycleMonitor.CaptureObservation(editorMode);
        }

        private static bool OnWantsToQuit ()
        {
            // NOTE:
            // wantsToQuit may be canceled by another handler returning false. shuttingDown must only become
            // observable after quitting is confirmed, otherwise execution requests could be rejected permanently.
            return true;
        }

        private static void OnQuitting ()
        {
            SharedLifecycleMonitor.OnShutdownStarted();
        }

        private static void OnPlayModeStateChanged (PlayModeStateChange stateChange)
        {
            SharedLifecycleMonitor.OnPlayModeStateChanged(stateChange);
        }

        private static void OnCompilationStarted (object _)
        {
            SharedLifecycleMonitor.OnCompilationStarted();
        }

        private static void OnAssemblyCompilationFinished (
            string _,
            CompilerMessage[] messages)
        {
            SharedLifecycleMonitor.OnAssemblyCompilationFinished(messages);
        }

        private static void OnCompilationFinished (object _)
        {
            SharedLifecycleMonitor.OnCompilationFinished();
        }

        private static void OnBeforeAssemblyReload ()
        {
            SharedLifecycleMonitor.OnBeforeAssemblyReload();
        }

        private static void OnAfterAssemblyReload ()
        {
            SharedLifecycleMonitor.OnAfterAssemblyReload();
        }

        private static void OnEditorUpdate ()
        {
            SharedLifecycleMonitor.ObserveEditorUpdate();
        }

        private sealed class ReadinessWaitState
        {
            private readonly UnityEditorReadinessGate readinessGate;

            private readonly CancellationToken cancellationToken;

            private readonly TaskScheduler mainThreadTaskScheduler;

            private readonly TaskCompletionSource<UnityEditorExecutionReadinessResult?> terminalResultSource =
                new TaskCompletionSource<UnityEditorExecutionReadinessResult?>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly TaskCompletionSource<UnityEditorExecutionReadinessResult> completionSource =
                new TaskCompletionSource<UnityEditorExecutionReadinessResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            private CancellationTokenRegistration cancellationRegistration;

            private int completionStarted;

            private int detachStarted;

            public ReadinessWaitState (
                UnityEditorReadinessGate readinessGate,
                CancellationToken cancellationToken)
            {
                this.readinessGate = readinessGate;
                this.cancellationToken = cancellationToken;
                _ = SynchronizationContext.Current
                    ?? throw new InvalidOperationException(
                        "Unity editor readiness waits must be attached from the Unity main-thread synchronization context.");
                mainThreadTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            }

            public Task<UnityEditorExecutionReadinessResult> AttachAndWaitAsync ()
            {
                try
                {
                    readinessGate.editorUpdateSubscriber(OnEditorUpdate);
                    readinessGate.beforeAssemblyReloadSubscriber(OnBeforeAssemblyReload);
                    readinessGate.quittingSubscriber(OnQuitting);
                    if (cancellationToken.CanBeCanceled)
                    {
                        // Cancellation callbacks can run on timer or transport threads. Keep the callback to one
                        // atomic notification; the terminal continuation performs Unity cleanup on the captured
                        // main-thread scheduler before completing the caller-visible task.
                        cancellationRegistration = cancellationToken.Register(static state =>
                        {
                            var waitState = (ReadinessWaitState)state!;
                            waitState.terminalResultSource.TrySetResult(null);
                        }, this);
                    }

                    _ = terminalResultSource.Task.ContinueWith(
                        static (terminalTask, state) =>
                        {
                            var waitState = (ReadinessWaitState)state!;
                            waitState.CompleteTerminalOnMainThread(terminalTask.Result);
                        },
                        this,
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        mainThreadTaskScheduler);
                    TryCompleteFromCurrentSnapshot();
                }
                catch
                {
                    _ = Interlocked.Exchange(ref completionStarted, 1);
                    Detach();
                    cancellationRegistration.Dispose();
                    throw;
                }

                return completionSource.Task;
            }

            private void OnEditorUpdate ()
            {
                readinessGate.lifecycleMonitor.ObserveEditorUpdate();
                var snapshot = readinessGate.CaptureEditorObservation();
                if (snapshot.CanAcceptExecutionRequests)
                {
                    Complete(UnityEditorExecutionReadinessResult.Ready(snapshot));
                    return;
                }

                if (!UnityEditorExecutionReadinessPolicy.IsWaitableState(snapshot.State.LifecycleState))
                {
                    Complete(UnityEditorExecutionReadinessPolicy.CreateBlockedResult(snapshot));
                }
            }

            private void TryCompleteFromCurrentSnapshot ()
            {
                var snapshot = readinessGate.CaptureEditorObservation();
                if (snapshot.CanAcceptExecutionRequests)
                {
                    Complete(UnityEditorExecutionReadinessResult.Ready(snapshot));
                    return;
                }

                if (!UnityEditorExecutionReadinessPolicy.IsWaitableState(snapshot.State.LifecycleState))
                {
                    Complete(UnityEditorExecutionReadinessPolicy.CreateBlockedResult(snapshot));
                }
            }

            private void OnBeforeAssemblyReload ()
            {
                // NOTE:
                // Pending readiness requests are not persisted across AppDomain reloads, so the gate must
                // complete with a blocked result before Unity tears down the current domain.
                CompleteBlocked(IpcEditorLifecycleState.DomainReloading);
            }

            private void OnQuitting ()
            {
                CompleteBlocked(IpcEditorLifecycleState.ShuttingDown);
            }

            private void Cancel ()
            {
                Detach();
                completionSource.TrySetCanceled(cancellationToken);
            }

            private void CompleteBlocked (IpcEditorLifecycleState lifecycleState)
            {
                Complete(CreateBlockedResult(lifecycleState));
            }

            private UnityEditorExecutionReadinessResult CreateBlockedResult (IpcEditorLifecycleState lifecycleState)
            {
                var snapshot = readinessGate.CaptureEditorObservation();
                var blockedSnapshot = snapshot.WithLifecycleState(lifecycleState);
                return UnityEditorExecutionReadinessPolicy.CreateBlockedResult(blockedSnapshot);
            }

            private void Complete (UnityEditorExecutionReadinessResult result)
            {
                if (terminalResultSource.TrySetResult(result))
                {
                    CompleteTerminalOnMainThread(result);
                    return;
                }

                CompleteTerminalOnMainThread(terminalResultSource.Task.GetAwaiter().GetResult());
            }

            private void CompleteTerminalOnMainThread (UnityEditorExecutionReadinessResult? result)
            {
                if (Interlocked.Exchange(ref completionStarted, 1) != 0)
                {
                    return;
                }

                Detach();
                // Completion always runs on the captured main-thread scheduler or a Unity lifecycle callback,
                // so this registration is never disposed from inside its own cancellation callback.
                cancellationRegistration.Dispose();
                if (result == null)
                {
                    completionSource.TrySetCanceled(cancellationToken);
                    return;
                }

                completionSource.TrySetResult(result);
            }

            private void Detach ()
            {
                if (Interlocked.Exchange(ref detachStarted, 1) != 0)
                {
                    return;
                }

                readinessGate.editorUpdateUnsubscriber(OnEditorUpdate);
                readinessGate.beforeAssemblyReloadUnsubscriber(OnBeforeAssemblyReload);
                readinessGate.quittingUnsubscriber(OnQuitting);
            }
        }
    }
}
