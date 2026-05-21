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

        private static readonly UnityEditorLifecycleMonitor sharedLifecycleMonitor = new UnityEditorLifecycleMonitor(
            sharedLifecycleTelemetryState,
            static () => EditorApplication.isCompiling,
            static () => EditorApplication.isUpdating,
            static () => EditorApplication.isPlayingOrWillChangePlaymode);

        private readonly UnityEditorLifecycleMonitor lifecycleMonitor;

        private readonly Func<bool> isPlayModeMutationActiveProvider;

        private readonly DaemonEditorMode editorMode;

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
                sharedLifecycleMonitor,
                static () => EditorApplication.isPlaying,
                static handler => AssemblyReloadEvents.beforeAssemblyReload += handler,
                static handler => AssemblyReloadEvents.beforeAssemblyReload -= handler,
                static handler => EditorApplication.quitting += handler,
                static handler => EditorApplication.quitting -= handler,
                subscribeToEditorEvents: true)
        {
        }

        /// <summary> Initializes a new instance of the <see cref="UnityEditorReadinessGate" /> class. </summary>
        /// <param name="editorMode"> The daemon Editor mode reported by lifecycle snapshots. </param>
        /// <param name="lifecycleMonitor"> The lifecycle monitor dependency. </param>
        /// <param name="isPlayModeMutationActiveProvider"> The active Play Mode observer used by Play Mode mutation readiness. </param>
        /// <param name="beforeAssemblyReloadSubscriber"> Subscribes one handler to the assembly-reload start event. </param>
        /// <param name="beforeAssemblyReloadUnsubscriber"> Unsubscribes one handler from the assembly-reload start event. </param>
        /// <param name="quittingSubscriber"> Subscribes one handler to the editor-quitting event. </param>
        /// <param name="quittingUnsubscriber"> Unsubscribes one handler from the editor-quitting event. </param>
        /// <param name="subscribeToEditorEvents"> Whether this instance should subscribe shared Unity editor lifecycle callbacks. </param>
        internal UnityEditorReadinessGate (
            DaemonEditorMode editorMode,
            UnityEditorLifecycleMonitor lifecycleMonitor,
            Func<bool> isPlayModeMutationActiveProvider,
            Action<AssemblyReloadEvents.AssemblyReloadCallback> beforeAssemblyReloadSubscriber,
            Action<AssemblyReloadEvents.AssemblyReloadCallback> beforeAssemblyReloadUnsubscriber,
            Action<Action> quittingSubscriber,
            Action<Action> quittingUnsubscriber,
            bool subscribeToEditorEvents)
        {
            this.lifecycleMonitor = lifecycleMonitor ?? throw new ArgumentNullException(nameof(lifecycleMonitor));
            this.isPlayModeMutationActiveProvider = isPlayModeMutationActiveProvider ?? throw new ArgumentNullException(nameof(isPlayModeMutationActiveProvider));
            _ = DaemonEditorModeCodec.ToValue(editorMode);
            this.editorMode = editorMode;
            this.beforeAssemblyReloadSubscriber = beforeAssemblyReloadSubscriber ?? throw new ArgumentNullException(nameof(beforeAssemblyReloadSubscriber));
            this.beforeAssemblyReloadUnsubscriber = beforeAssemblyReloadUnsubscriber ?? throw new ArgumentNullException(nameof(beforeAssemblyReloadUnsubscriber));
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
            EditorApplication.wantsToQuit -= OnWantsToQuit;
            EditorApplication.quitting -= OnQuitting;

            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
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
            return lifecycleMonitor.CaptureSnapshot(editorMode);
        }

        /// <inheritdoc />
        public Task<UnityEditorExecutionReadinessResult> EnsureExecutionReadyAsync (
            bool failFast,
            CancellationToken cancellationToken = default,
            bool allowPlayMode = false)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var snapshot = CaptureSnapshot();
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

            if (failFast || !UnityEditorExecutionReadinessPolicy.IsWaitableState(snapshot.LifecycleState))
            {
                return Task.FromResult(UnityEditorExecutionReadinessPolicy.CreateBlockedResult(snapshot));
            }

            var waitState = new ReadinessWaitState(this, cancellationToken);
            return waitState.AttachAndWaitAsync();
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
            sharedLifecycleMonitor.OnShutdownStarted();
        }

        private static void OnCompilationStarted (object _)
        {
            sharedLifecycleMonitor.OnCompilationStarted();
        }

        private static void OnAssemblyCompilationFinished (
            string _,
            CompilerMessage[] messages)
        {
            sharedLifecycleMonitor.OnAssemblyCompilationFinished(messages);
        }

        private static void OnCompilationFinished (object _)
        {
            sharedLifecycleMonitor.OnCompilationFinished();
        }

        private static void OnBeforeAssemblyReload ()
        {
            sharedLifecycleMonitor.OnBeforeAssemblyReload();
        }

        private static void OnAfterAssemblyReload ()
        {
            sharedLifecycleMonitor.OnAfterAssemblyReload();
        }

        private static void OnEditorUpdate ()
        {
            sharedLifecycleMonitor.ObserveEditorUpdate();
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

            public Task<UnityEditorExecutionReadinessResult> AttachAndWaitAsync ()
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
                readinessGate.lifecycleMonitor.ObserveEditorUpdate();
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
                    CanAcceptExecutionRequests: false,
                    ObservedAtUtc: snapshot.ObservedAtUtc,
                    ActionRequired: snapshot.ActionRequired,
                    PrimaryDiagnostic: snapshot.PrimaryDiagnostic);
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
