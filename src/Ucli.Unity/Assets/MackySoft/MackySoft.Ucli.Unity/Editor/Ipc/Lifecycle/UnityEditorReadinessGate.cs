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
        private static int compileGeneration;

        private static int domainReloadGeneration = UnityEditorDomainReloadGenerationStore.Restore();

        private static bool isDomainReloading;

        private static bool isShuttingDown;

        private static bool isStartupPending = true;

        /// <summary> Gets a value indicating whether the editor is ready to start IPC host bootstrap. </summary>
        internal static bool IsReadyForBootstrapStartup => !EditorApplication.isCompiling;

        /// <summary> Initializes a new instance of the <see cref="UnityEditorReadinessGate" /> class. </summary>
        public UnityEditorReadinessGate ()
        {
            CompilationPipeline.compilationStarted -= OnCompilationStarted;
            CompilationPipeline.compilationFinished -= OnCompilationFinished;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
            EditorApplication.wantsToQuit -= OnWantsToQuit;
            EditorApplication.quitting -= OnQuitting;

            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
            EditorApplication.wantsToQuit += OnWantsToQuit;
            EditorApplication.quitting += OnQuitting;
        }

        /// <inheritdoc />
        public UnityEditorLifecycleSnapshot CaptureSnapshot ()
        {
            var compileState = IpcCompileStateCodec.ToValue(EditorApplication.isCompiling);
            var lifecycleState = ResolveLifecycleState();
            var blockingReason = ResolveBlockingReason(lifecycleState);
            var canAcceptExecutionRequests = string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.Ready, System.StringComparison.Ordinal);
            if (canAcceptExecutionRequests)
            {
                isStartupPending = false;
            }

            // TODO: Add GUI / non-batchmode runtime detection when lifecycle telemetry is supported
            // outside the batchmode IPC host. Current runtime reporting is batchmode-only.
            return new UnityEditorLifecycleSnapshot(
                Runtime: "batchmode",
                LifecycleState: lifecycleState,
                BlockingReason: blockingReason,
                CompileState: compileState,
                CompileGeneration: Volatile.Read(ref compileGeneration).ToString(System.Globalization.CultureInfo.InvariantCulture),
                DomainReloadGeneration: Volatile.Read(ref domainReloadGeneration).ToString(System.Globalization.CultureInfo.InvariantCulture),
                CanAcceptExecutionRequests: canAcceptExecutionRequests);
        }

        /// <inheritdoc />
        public Task<UnityEditorExecutionReadinessResult> EnsureExecutionReady (
            bool waitUntilReady,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var snapshot = CaptureSnapshot();
            if (snapshot.CanAcceptExecutionRequests)
            {
                return Task.FromResult(UnityEditorExecutionReadinessResult.Ready(snapshot));
            }

            if (!waitUntilReady || !IsWaitableState(snapshot.LifecycleState))
            {
                return Task.FromResult(CreateBlockedResult(snapshot));
            }

            var waitState = new ReadinessWaitState(this, cancellationToken);
            return waitState.AttachAndWait();
        }

        /// <summary> Gets the current domain-reload generation used by plan-token environment snapshots. </summary>
        internal static string CurrentDomainReloadGeneration => Volatile.Read(ref domainReloadGeneration).ToString(System.Globalization.CultureInfo.InvariantCulture);

        private static bool OnWantsToQuit ()
        {
            isShuttingDown = true;
            return true;
        }

        private static void OnQuitting ()
        {
            isShuttingDown = true;
        }

        private static void OnCompilationStarted (object _)
        {
            Interlocked.Increment(ref compileGeneration);
            isStartupPending = true;
        }

        private static void OnCompilationFinished (object _)
        {
            Interlocked.Increment(ref compileGeneration);
        }

        private static void OnBeforeAssemblyReload ()
        {
            isDomainReloading = true;
            isStartupPending = true;
            Interlocked.Exchange(
                ref domainReloadGeneration,
                UnityEditorDomainReloadGenerationStore.Advance(Volatile.Read(ref domainReloadGeneration)));
        }

        private static void OnAfterAssemblyReload ()
        {
            isDomainReloading = false;
            domainReloadGeneration = UnityEditorDomainReloadGenerationStore.Restore();
            isStartupPending = true;
        }

        private static bool IsWaitableState (string lifecycleState)
        {
            return string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.Starting, System.StringComparison.Ordinal)
                || string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.Busy, System.StringComparison.Ordinal)
                || string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.Compiling, System.StringComparison.Ordinal)
                || string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.DomainReloading, System.StringComparison.Ordinal);
        }

        private static string? ResolveBlockingReason (string lifecycleState)
        {
            return lifecycleState switch
            {
                IpcEditorLifecycleStateCodec.Starting => IpcEditorBlockingReasonCodec.Startup,
                IpcEditorLifecycleStateCodec.Busy => IpcEditorBlockingReasonCodec.Busy,
                IpcEditorLifecycleStateCodec.Compiling => IpcEditorBlockingReasonCodec.Compile,
                IpcEditorLifecycleStateCodec.DomainReloading => IpcEditorBlockingReasonCodec.DomainReload,
                IpcEditorLifecycleStateCodec.Playmode => IpcEditorBlockingReasonCodec.PlayMode,
                IpcEditorLifecycleStateCodec.BlockedByModal => IpcEditorBlockingReasonCodec.ModalDialog,
                IpcEditorLifecycleStateCodec.SafeMode => IpcEditorBlockingReasonCodec.SafeMode,
                IpcEditorLifecycleStateCodec.ShuttingDown => IpcEditorBlockingReasonCodec.Shutdown,
                _ => null,
            };
        }

        private static string ResolveLifecycleState ()
        {
            // TODO: GUI / non-batchmode lifecycle support needs dedicated observation paths for
            // Play Mode, Safe Mode, and modal dialogs. Batchmode daemon only reports states that
            // are observable through public APIs today, so blockedByModal/safeMode/playmode are
            // reserved literals but do not transition here yet.
            return UnityEditorLifecycleStateResolver.Resolve(
                ref isStartupPending,
                isShuttingDown,
                isDomainReloading,
                EditorApplication.isCompiling,
                EditorApplication.isUpdating);
        }

        private static UnityEditorExecutionReadinessResult CreateBlockedResult (UnityEditorLifecycleSnapshot snapshot)
        {
            var error = snapshot.LifecycleState switch
            {
                IpcEditorLifecycleStateCodec.Starting => new IpcError(
                    IpcErrorCodes.EditorStarting,
                    "Unity editor startup is still in progress. Retry with --waitUntilReady or wait until lifecycleState=ready before executing request.",
                    null),
                IpcEditorLifecycleStateCodec.Busy => new IpcError(
                    IpcErrorCodes.EditorBusy,
                    "Unity editor is busy with internal work. Retry with --waitUntilReady or wait until lifecycleState=ready before executing request.",
                    null),
                IpcEditorLifecycleStateCodec.Compiling => new IpcError(
                    IpcErrorCodes.EditorCompiling,
                    "Unity editor is compiling scripts. Retry with --waitUntilReady or wait until lifecycleState=ready before executing request.",
                    null),
                IpcEditorLifecycleStateCodec.DomainReloading => new IpcError(
                    IpcErrorCodes.EditorDomainReloading,
                    "Unity editor is reloading the AppDomain. Retry with --waitUntilReady or wait until lifecycleState=ready before executing request.",
                    null),
                IpcEditorLifecycleStateCodec.Playmode => new IpcError(
                    IpcErrorCodes.EditorPlaymode,
                    "Unity editor is in Play Mode. Exit Play Mode and wait until lifecycleState=ready before executing request.",
                    null),
                IpcEditorLifecycleStateCodec.BlockedByModal => new IpcError(
                    IpcErrorCodes.EditorModalBlocked,
                    "Unity editor is blocked by a modal dialog. Resolve the dialog and wait until lifecycleState=ready before executing request.",
                    null),
                IpcEditorLifecycleStateCodec.SafeMode => new IpcError(
                    IpcErrorCodes.EditorSafeMode,
                    "Unity editor is in Safe Mode. Resolve compiler errors and wait until lifecycleState=ready before executing request.",
                    null),
                IpcEditorLifecycleStateCodec.ShuttingDown => new IpcError(
                    IpcErrorCodes.EditorShuttingDown,
                    "Unity editor is shutting down and cannot accept execution requests.",
                    null),
                _ => new IpcError(
                    IpcErrorCodes.InternalError,
                    $"Unity editor lifecycle gate returned unsupported state '{snapshot.LifecycleState}'.",
                    null),
            };

            return UnityEditorExecutionReadinessResult.Blocked(snapshot, error);
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
                var snapshot = readinessGate.CaptureSnapshot();
                if (snapshot.CanAcceptExecutionRequests)
                {
                    Detach();
                    completionSource.TrySetResult(UnityEditorExecutionReadinessResult.Ready(snapshot));
                    return;
                }

                if (!IsWaitableState(snapshot.LifecycleState))
                {
                    Detach();
                    completionSource.TrySetResult(CreateBlockedResult(snapshot));
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
