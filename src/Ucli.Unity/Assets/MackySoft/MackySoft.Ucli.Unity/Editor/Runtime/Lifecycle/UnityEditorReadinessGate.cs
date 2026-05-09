using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using UnityEditor;
using UnityEditor.Compilation;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Captures Unity editor lifecycle telemetry and gates execution requests. </summary>
    internal sealed class UnityEditorReadinessGate : IUnityEditorReadinessGate
    {
        private static readonly UnityEditorLifecycleTelemetryState sharedLifecycleTelemetryState = new UnityEditorLifecycleTelemetryState();

        private readonly UnityEditorLifecycleTelemetryState lifecycleTelemetryState;

        private readonly DaemonEditorMode editorMode;

        private readonly Func<bool> isCompilingProvider;

        private readonly Func<bool> isUpdatingProvider;

        private readonly Func<bool> isPlaymodeActiveProvider;

        private readonly Action<AssemblyReloadEvents.AssemblyReloadCallback> beforeAssemblyReloadSubscriber;

        private readonly Action<AssemblyReloadEvents.AssemblyReloadCallback> beforeAssemblyReloadUnsubscriber;

        private readonly Action<Action> quittingSubscriber;

        private readonly Action<Action> quittingUnsubscriber;

        /// <summary> Gets a value indicating whether the editor is ready to start IPC host bootstrap. </summary>
        internal static bool IsReadyForBootstrapStartup => !EditorApplication.isCompiling;

        /// <summary> Initializes a new instance of the <see cref="UnityEditorReadinessGate" /> class. </summary>
        public UnityEditorReadinessGate ()
            : this(DaemonEditorMode.Batchmode)
        {
        }

        /// <summary> Initializes a new instance of the <see cref="UnityEditorReadinessGate" /> class. </summary>
        /// <param name="editorMode"> The daemon Editor mode reported by lifecycle snapshots. </param>
        public UnityEditorReadinessGate (DaemonEditorMode editorMode)
            : this(
                editorMode,
                sharedLifecycleTelemetryState,
                static () => EditorApplication.isCompiling,
                static () => EditorApplication.isUpdating,
                static () => EditorApplication.isPlayingOrWillChangePlaymode,
                static handler => AssemblyReloadEvents.beforeAssemblyReload += handler,
                static handler => AssemblyReloadEvents.beforeAssemblyReload -= handler,
                static handler => EditorApplication.quitting += handler,
                static handler => EditorApplication.quitting -= handler,
                subscribeToEditorEvents: true)
        {
        }

        /// <summary> Initializes a new instance of the <see cref="UnityEditorReadinessGate" /> class. </summary>
        /// <param name="lifecycleTelemetryState"> The mutable lifecycle telemetry state to observe. </param>
        internal UnityEditorReadinessGate (UnityEditorLifecycleTelemetryState lifecycleTelemetryState)
            : this(
                DaemonEditorMode.Batchmode,
                lifecycleTelemetryState,
                static () => EditorApplication.isCompiling,
                static () => EditorApplication.isUpdating,
                static () => EditorApplication.isPlayingOrWillChangePlaymode,
                static handler => AssemblyReloadEvents.beforeAssemblyReload += handler,
                static handler => AssemblyReloadEvents.beforeAssemblyReload -= handler,
                static handler => EditorApplication.quitting += handler,
                static handler => EditorApplication.quitting -= handler,
                subscribeToEditorEvents: false)
        {
        }

        /// <summary> Initializes a new instance of the <see cref="UnityEditorReadinessGate" /> class. </summary>
        /// <param name="lifecycleTelemetryState"> The mutable lifecycle telemetry state to observe. </param>
        /// <param name="isCompilingProvider"> The compile-state observer. </param>
        /// <param name="isUpdatingProvider"> The update-state observer. </param>
        /// <param name="isPlaymodeActiveProvider"> The Play Mode observer. </param>
        internal UnityEditorReadinessGate (
            UnityEditorLifecycleTelemetryState lifecycleTelemetryState,
            Func<bool> isCompilingProvider,
            Func<bool> isUpdatingProvider,
            Func<bool> isPlaymodeActiveProvider)
            : this(
                DaemonEditorMode.Batchmode,
                lifecycleTelemetryState,
                isCompilingProvider,
                isUpdatingProvider,
                isPlaymodeActiveProvider,
                static handler => AssemblyReloadEvents.beforeAssemblyReload += handler,
                static handler => AssemblyReloadEvents.beforeAssemblyReload -= handler,
                static handler => EditorApplication.quitting += handler,
                static handler => EditorApplication.quitting -= handler,
                subscribeToEditorEvents: false)
        {
        }

        /// <summary> Initializes a new instance of the <see cref="UnityEditorReadinessGate" /> class. </summary>
        /// <param name="editorMode"> The daemon Editor mode reported by lifecycle snapshots. </param>
        /// <param name="lifecycleTelemetryState"> The mutable lifecycle telemetry state to observe. </param>
        /// <param name="isCompilingProvider"> The compile-state observer. </param>
        /// <param name="isUpdatingProvider"> The update-state observer. </param>
        /// <param name="isPlaymodeActiveProvider"> The Play Mode observer. </param>
        internal UnityEditorReadinessGate (
            DaemonEditorMode editorMode,
            UnityEditorLifecycleTelemetryState lifecycleTelemetryState,
            Func<bool> isCompilingProvider,
            Func<bool> isUpdatingProvider,
            Func<bool> isPlaymodeActiveProvider)
            : this(
                editorMode,
                lifecycleTelemetryState,
                isCompilingProvider,
                isUpdatingProvider,
                isPlaymodeActiveProvider,
                static handler => AssemblyReloadEvents.beforeAssemblyReload += handler,
                static handler => AssemblyReloadEvents.beforeAssemblyReload -= handler,
                static handler => EditorApplication.quitting += handler,
                static handler => EditorApplication.quitting -= handler,
                subscribeToEditorEvents: false)
        {
        }

        /// <summary> Initializes a new instance of the <see cref="UnityEditorReadinessGate" /> class. </summary>
        /// <param name="lifecycleTelemetryState"> The mutable lifecycle telemetry state to observe. </param>
        /// <param name="isCompilingProvider"> The compile-state observer. </param>
        /// <param name="isUpdatingProvider"> The update-state observer. </param>
        /// <param name="isPlaymodeActiveProvider"> The Play Mode observer. </param>
        /// <param name="beforeAssemblyReloadSubscriber"> Subscribes one handler to the assembly-reload start event. </param>
        /// <param name="beforeAssemblyReloadUnsubscriber"> Unsubscribes one handler from the assembly-reload start event. </param>
        /// <param name="quittingSubscriber"> Subscribes one handler to the editor-quitting event. </param>
        /// <param name="quittingUnsubscriber"> Unsubscribes one handler from the editor-quitting event. </param>
        internal UnityEditorReadinessGate (
            UnityEditorLifecycleTelemetryState lifecycleTelemetryState,
            Func<bool> isCompilingProvider,
            Func<bool> isUpdatingProvider,
            Func<bool> isPlaymodeActiveProvider,
            Action<AssemblyReloadEvents.AssemblyReloadCallback> beforeAssemblyReloadSubscriber,
            Action<AssemblyReloadEvents.AssemblyReloadCallback> beforeAssemblyReloadUnsubscriber,
            Action<Action> quittingSubscriber,
            Action<Action> quittingUnsubscriber)
            : this(
                DaemonEditorMode.Batchmode,
                lifecycleTelemetryState,
                isCompilingProvider,
                isUpdatingProvider,
                isPlaymodeActiveProvider,
                beforeAssemblyReloadSubscriber,
                beforeAssemblyReloadUnsubscriber,
                quittingSubscriber,
                quittingUnsubscriber,
                subscribeToEditorEvents: false)
        {
        }

        private UnityEditorReadinessGate (
            DaemonEditorMode editorMode,
            UnityEditorLifecycleTelemetryState lifecycleTelemetryState,
            Func<bool> isCompilingProvider,
            Func<bool> isUpdatingProvider,
            Func<bool> isPlaymodeActiveProvider,
            Action<AssemblyReloadEvents.AssemblyReloadCallback> beforeAssemblyReloadSubscriber,
            Action<AssemblyReloadEvents.AssemblyReloadCallback> beforeAssemblyReloadUnsubscriber,
            Action<Action> quittingSubscriber,
            Action<Action> quittingUnsubscriber,
            bool subscribeToEditorEvents)
        {
            this.lifecycleTelemetryState = lifecycleTelemetryState;
            _ = DaemonEditorModeCodec.ToValue(editorMode);
            this.editorMode = editorMode;
            this.isCompilingProvider = isCompilingProvider ?? throw new ArgumentNullException(nameof(isCompilingProvider));
            this.isUpdatingProvider = isUpdatingProvider ?? throw new ArgumentNullException(nameof(isUpdatingProvider));
            this.isPlaymodeActiveProvider = isPlaymodeActiveProvider ?? throw new ArgumentNullException(nameof(isPlaymodeActiveProvider));
            this.beforeAssemblyReloadSubscriber = beforeAssemblyReloadSubscriber ?? throw new ArgumentNullException(nameof(beforeAssemblyReloadSubscriber));
            this.beforeAssemblyReloadUnsubscriber = beforeAssemblyReloadUnsubscriber ?? throw new ArgumentNullException(nameof(beforeAssemblyReloadUnsubscriber));
            this.quittingSubscriber = quittingSubscriber ?? throw new ArgumentNullException(nameof(quittingSubscriber));
            this.quittingUnsubscriber = quittingUnsubscriber ?? throw new ArgumentNullException(nameof(quittingUnsubscriber));
            if (!subscribeToEditorEvents)
            {
                return;
            }

            CompilationPipeline.compilationStarted -= OnCompilationStarted;
            CompilationPipeline.compilationFinished -= OnCompilationFinished;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.wantsToQuit -= OnWantsToQuit;
            EditorApplication.quitting -= OnQuitting;

            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.wantsToQuit += OnWantsToQuit;
            EditorApplication.quitting += OnQuitting;
        }

        /// <inheritdoc />
        public UnityEditorLifecycleSnapshot CaptureSnapshot ()
        {
            var isCompiling = isCompilingProvider();
            var isUpdating = isUpdatingProvider();
            var isPlaymodeActive = isPlaymodeActiveProvider();
            var compileState = IpcCompileStateCodec.ToValue(isCompiling);

            // TODO: GUI / non-batchmode lifecycle support needs dedicated observation paths for
            // Safe Mode and modal dialogs. Batchmode daemon only reports states that are observable
            // through public APIs today, so blockedByModal/safeMode remain reserved literals here.
            var lifecycleState = lifecycleTelemetryState.ResolveLifecycleState(isPlaymodeActive, isCompiling, isUpdating);
            var blockingReason = UnityEditorExecutionReadinessPolicy.ResolveBlockingReason(lifecycleState);
            var canAcceptExecutionRequests = string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.Ready, System.StringComparison.Ordinal);

            return new UnityEditorLifecycleSnapshot(
                EditorMode: editorMode,
                LifecycleState: lifecycleState,
                BlockingReason: blockingReason,
                CompileState: compileState,
                CompileGeneration: lifecycleTelemetryState.CompileGeneration,
                DomainReloadGeneration: lifecycleTelemetryState.DomainReloadGeneration,
                CanAcceptExecutionRequests: canAcceptExecutionRequests);
        }

        /// <inheritdoc />
        public Task<UnityEditorExecutionReadinessResult> EnsureExecutionReady (
            bool failFast,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var snapshot = CaptureSnapshot();
            if (snapshot.CanAcceptExecutionRequests)
            {
                return Task.FromResult(UnityEditorExecutionReadinessResult.Ready(snapshot));
            }

            if (failFast || !UnityEditorExecutionReadinessPolicy.IsWaitableState(snapshot.LifecycleState))
            {
                return Task.FromResult(UnityEditorExecutionReadinessPolicy.CreateBlockedResult(snapshot));
            }

            var waitState = new ReadinessWaitState(this, cancellationToken);
            return waitState.AttachAndWait();
        }

        /// <summary> Gets the current domain-reload generation used by plan-token environment snapshots. </summary>
        internal static string CurrentDomainReloadGeneration => sharedLifecycleTelemetryState.DomainReloadGeneration;

        private static bool OnWantsToQuit ()
        {
            // NOTE:
            // wantsToQuit may be canceled by another handler returning false. shuttingDown must only become
            // observable after quitting is confirmed, otherwise execution requests could be rejected permanently.
            return true;
        }

        private static void OnQuitting ()
        {
            sharedLifecycleTelemetryState.OnShutdownStarted();
        }

        private static void OnCompilationStarted (object _)
        {
            sharedLifecycleTelemetryState.OnCompilationStarted();
        }

        private static void OnCompilationFinished (object _)
        {
            sharedLifecycleTelemetryState.OnCompilationFinished();
        }

        private static void OnBeforeAssemblyReload ()
        {
            sharedLifecycleTelemetryState.OnBeforeAssemblyReload();
        }

        private static void OnAfterAssemblyReload ()
        {
            sharedLifecycleTelemetryState.OnAfterAssemblyReload();
        }

        private static void OnEditorUpdate ()
        {
            sharedLifecycleTelemetryState.ObserveEditorUpdate(
                EditorApplication.isPlayingOrWillChangePlaymode,
                EditorApplication.isCompiling,
                EditorApplication.isUpdating);
        }

        private sealed class ReadinessWaitState
        {
            private readonly UnityEditorReadinessGate readinessGate;

            private readonly CancellationToken cancellationToken;

            private readonly TaskCompletionSource<UnityEditorExecutionReadinessResult> completionSource =
                new TaskCompletionSource<UnityEditorExecutionReadinessResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            private CancellationTokenRegistration cancellationRegistration;

            private bool isDetached;

            public ReadinessWaitState (
                UnityEditorReadinessGate readinessGate,
                CancellationToken cancellationToken)
            {
                this.readinessGate = readinessGate;
                this.cancellationToken = cancellationToken;
            }

            public Task<UnityEditorExecutionReadinessResult> AttachAndWait ()
            {
                EditorApplication.update += OnEditorUpdate;
                readinessGate.beforeAssemblyReloadSubscriber(OnBeforeAssemblyReload);
                readinessGate.quittingSubscriber(OnQuitting);
                if (cancellationToken.CanBeCanceled)
                {
                    cancellationRegistration = cancellationToken.Register(static state =>
                    {
                        var waitState = (ReadinessWaitState)state!;
                        waitState.Cancel();
                    }, this);
                }

                TryCompleteFromCurrentSnapshot();
                return completionSource.Task;
            }

            private void OnEditorUpdate ()
            {
                readinessGate.lifecycleTelemetryState.ObserveEditorUpdate(
                    readinessGate.isPlaymodeActiveProvider(),
                    readinessGate.isCompilingProvider(),
                    readinessGate.isUpdatingProvider());
                var snapshot = readinessGate.CaptureSnapshot();
                if (snapshot.CanAcceptExecutionRequests)
                {
                    Detach();
                    completionSource.TrySetResult(UnityEditorExecutionReadinessResult.Ready(snapshot));
                    return;
                }

                if (!UnityEditorExecutionReadinessPolicy.IsWaitableState(snapshot.LifecycleState))
                {
                    Detach();
                    completionSource.TrySetResult(UnityEditorExecutionReadinessPolicy.CreateBlockedResult(snapshot));
                }
            }

            private void TryCompleteFromCurrentSnapshot ()
            {
                var snapshot = readinessGate.CaptureSnapshot();
                if (snapshot.CanAcceptExecutionRequests)
                {
                    Detach();
                    completionSource.TrySetResult(UnityEditorExecutionReadinessResult.Ready(snapshot));
                    return;
                }

                if (!UnityEditorExecutionReadinessPolicy.IsWaitableState(snapshot.LifecycleState))
                {
                    Detach();
                    completionSource.TrySetResult(UnityEditorExecutionReadinessPolicy.CreateBlockedResult(snapshot));
                }
            }

            private void OnBeforeAssemblyReload ()
            {
                // NOTE:
                // Waited requests are not persisted across AppDomain reloads, so the gate must
                // complete with a blocked result before Unity tears down the current domain.
                CompleteBlocked(IpcEditorLifecycleStateCodec.DomainReloading);
            }

            private void OnQuitting ()
            {
                CompleteBlocked(IpcEditorLifecycleStateCodec.ShuttingDown);
            }

            private void Cancel ()
            {
                Detach();
                completionSource.TrySetCanceled(cancellationToken);
            }

            private void CompleteBlocked (string lifecycleState)
            {
                Detach();
                completionSource.TrySetResult(CreateBlockedResult(lifecycleState));
            }

            private UnityEditorExecutionReadinessResult CreateBlockedResult (string lifecycleState)
            {
                var snapshot = readinessGate.CaptureSnapshot();
                var blockedSnapshot = new UnityEditorLifecycleSnapshot(
                    EditorMode: snapshot.EditorMode,
                    LifecycleState: lifecycleState,
                    BlockingReason: UnityEditorExecutionReadinessPolicy.ResolveBlockingReason(lifecycleState),
                    CompileState: snapshot.CompileState,
                    CompileGeneration: snapshot.CompileGeneration,
                    DomainReloadGeneration: snapshot.DomainReloadGeneration,
                    CanAcceptExecutionRequests: false);
                return UnityEditorExecutionReadinessPolicy.CreateBlockedResult(blockedSnapshot);
            }

            private void Detach ()
            {
                if (isDetached)
                {
                    return;
                }

                isDetached = true;
                EditorApplication.update -= OnEditorUpdate;
                readinessGate.beforeAssemblyReloadUnsubscriber(OnBeforeAssemblyReload);
                readinessGate.quittingUnsubscriber(OnQuitting);
                cancellationRegistration.Dispose();
            }
        }
    }
}
