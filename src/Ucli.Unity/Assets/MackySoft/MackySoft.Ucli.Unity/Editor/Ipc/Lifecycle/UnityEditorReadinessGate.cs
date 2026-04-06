using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using UnityEditor;
using UnityEditor.Compilation;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Captures batchmode editor lifecycle telemetry and gates execution requests. </summary>
    internal sealed class UnityEditorReadinessGate : IUnityEditorReadinessGate
    {
        private static readonly UnityEditorLifecycleTelemetryState sharedLifecycleTelemetryState = new UnityEditorLifecycleTelemetryState();

        private readonly UnityEditorLifecycleTelemetryState lifecycleTelemetryState;

        private readonly Func<bool> isCompilingProvider;

        private readonly Func<bool> isUpdatingProvider;

        /// <summary> Gets a value indicating whether the editor is ready to start IPC host bootstrap. </summary>
        internal static bool IsReadyForBootstrapStartup => !EditorApplication.isCompiling;

        /// <summary> Initializes a new instance of the <see cref="UnityEditorReadinessGate" /> class. </summary>
        public UnityEditorReadinessGate ()
            : this(
                sharedLifecycleTelemetryState,
                static () => EditorApplication.isCompiling,
                static () => EditorApplication.isUpdating,
                subscribeToEditorEvents: true)
        {
        }

        /// <summary> Initializes a new instance of the <see cref="UnityEditorReadinessGate" /> class. </summary>
        /// <param name="lifecycleTelemetryState"> The mutable lifecycle telemetry state to observe. </param>
        internal UnityEditorReadinessGate (UnityEditorLifecycleTelemetryState lifecycleTelemetryState)
            : this(
                lifecycleTelemetryState,
                static () => EditorApplication.isCompiling,
                static () => EditorApplication.isUpdating,
                subscribeToEditorEvents: false)
        {
        }

        /// <summary> Initializes a new instance of the <see cref="UnityEditorReadinessGate" /> class. </summary>
        /// <param name="lifecycleTelemetryState"> The mutable lifecycle telemetry state to observe. </param>
        /// <param name="isCompilingProvider"> The compile-state observer. </param>
        /// <param name="isUpdatingProvider"> The update-state observer. </param>
        internal UnityEditorReadinessGate (
            UnityEditorLifecycleTelemetryState lifecycleTelemetryState,
            Func<bool> isCompilingProvider,
            Func<bool> isUpdatingProvider)
            : this(
                lifecycleTelemetryState,
                isCompilingProvider,
                isUpdatingProvider,
                subscribeToEditorEvents: false)
        {
        }

        private UnityEditorReadinessGate (
            UnityEditorLifecycleTelemetryState lifecycleTelemetryState,
            Func<bool> isCompilingProvider,
            Func<bool> isUpdatingProvider,
            bool subscribeToEditorEvents)
        {
            this.lifecycleTelemetryState = lifecycleTelemetryState;
            this.isCompilingProvider = isCompilingProvider ?? throw new ArgumentNullException(nameof(isCompilingProvider));
            this.isUpdatingProvider = isUpdatingProvider ?? throw new ArgumentNullException(nameof(isUpdatingProvider));
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
            var compileState = IpcCompileStateCodec.ToValue(isCompiling);

            // TODO: GUI / non-batchmode lifecycle support needs dedicated observation paths for
            // Play Mode, Safe Mode, and modal dialogs. Batchmode daemon only reports states that
            // are observable through public APIs today, so blockedByModal/safeMode/playmode are
            // reserved literals but do not transition here yet.
            var lifecycleState = lifecycleTelemetryState.ResolveLifecycleState(isCompiling, isUpdating);
            var blockingReason = UnityEditorExecutionReadinessPolicy.ResolveBlockingReason(lifecycleState);
            var canAcceptExecutionRequests = string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.Ready, System.StringComparison.Ordinal);

            // TODO: Add GUI / non-batchmode runtime detection when lifecycle telemetry is supported
            // outside the batchmode IPC host. Current runtime reporting is batchmode-only.
            return new UnityEditorLifecycleSnapshot(
                Runtime: IpcEditorRuntimeCodec.Batchmode,
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
            sharedLifecycleTelemetryState.OnShutdownStarted();
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
            sharedLifecycleTelemetryState.ObserveEditorUpdate(EditorApplication.isCompiling, EditorApplication.isUpdating);
        }

        private sealed class ReadinessWaitState
        {
            private readonly UnityEditorReadinessGate readinessGate;

            private readonly CancellationToken cancellationToken;

            private readonly TaskCompletionSource<UnityEditorExecutionReadinessResult> completionSource =
                new TaskCompletionSource<UnityEditorExecutionReadinessResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            private CancellationTokenRegistration cancellationRegistration;

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
                if (cancellationToken.CanBeCanceled)
                {
                    cancellationRegistration = cancellationToken.Register(static state =>
                    {
                        var waitState = (ReadinessWaitState)state!;
                        waitState.Cancel();
                    }, this);
                }

                OnEditorUpdate();
                return completionSource.Task;
            }

            private void OnEditorUpdate ()
            {
                readinessGate.lifecycleTelemetryState.ObserveEditorUpdate(
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

            private void Cancel ()
            {
                Detach();
                completionSource.TrySetCanceled(cancellationToken);
            }

            private void Detach ()
            {
                EditorApplication.update -= OnEditorUpdate;
                cancellationRegistration.Dispose();
            }
        }
    }
}
