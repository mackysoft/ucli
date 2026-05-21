using System;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using UnityEditor.Compilation;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Observes Unity callbacks and produces lifecycle snapshots for IPC readiness decisions. </summary>
    internal sealed class UnityEditorLifecycleMonitor
    {
        private readonly UnityEditorLifecycleTelemetryState lifecycleTelemetryState;

        private readonly Func<bool> isCompilingProvider;

        private readonly Func<bool> isUpdatingProvider;

        private readonly Func<bool> isPlayingProvider;

        private readonly Func<bool> isPlayingOrWillChangePlaymodeProvider;

        /// <summary> Initializes a new instance of the <see cref="UnityEditorLifecycleMonitor" /> class. </summary>
        public UnityEditorLifecycleMonitor (
            UnityEditorLifecycleTelemetryState lifecycleTelemetryState,
            Func<bool> isCompilingProvider,
            Func<bool> isUpdatingProvider,
            Func<bool> isPlayingProvider,
            Func<bool> isPlayingOrWillChangePlaymodeProvider)
        {
            this.lifecycleTelemetryState = lifecycleTelemetryState ?? throw new ArgumentNullException(nameof(lifecycleTelemetryState));
            this.isCompilingProvider = isCompilingProvider ?? throw new ArgumentNullException(nameof(isCompilingProvider));
            this.isUpdatingProvider = isUpdatingProvider ?? throw new ArgumentNullException(nameof(isUpdatingProvider));
            this.isPlayingProvider = isPlayingProvider ?? throw new ArgumentNullException(nameof(isPlayingProvider));
            this.isPlayingOrWillChangePlaymodeProvider = isPlayingOrWillChangePlaymodeProvider ?? throw new ArgumentNullException(nameof(isPlayingOrWillChangePlaymodeProvider));
        }

        /// <summary> Captures one lifecycle snapshot for the specified daemon editor mode. </summary>
        public UnityEditorLifecycleSnapshot CaptureSnapshot (DaemonEditorMode editorMode)
        {
            var isCompiling = isCompilingProvider();
            var isUpdating = isUpdatingProvider();
            var isPlaying = isPlayingProvider();
            var isPlayingOrWillChangePlaymode = isPlayingOrWillChangePlaymodeProvider();
            var lifecycleState = lifecycleTelemetryState.ResolveLifecycleState(isPlayingOrWillChangePlaymode, isCompiling, isUpdating);
            var blockingReason = UnityEditorExecutionReadinessPolicy.ResolveBlockingReason(lifecycleState);
            var canAcceptExecutionRequests = string.Equals(lifecycleState, IpcEditorLifecycleStateCodec.Ready, StringComparison.Ordinal);

            return new UnityEditorLifecycleSnapshot(
                EditorMode: editorMode,
                LifecycleState: lifecycleState,
                BlockingReason: blockingReason,
                CompileState: IpcCompileStateCodec.ToValue(isCompiling, lifecycleTelemetryState.HasCompileFailure),
                CompileGeneration: lifecycleTelemetryState.CompileGeneration,
                DomainReloadGeneration: lifecycleTelemetryState.DomainReloadGeneration,
                CanAcceptExecutionRequests: canAcceptExecutionRequests,
                ObservedAtUtc: DateTimeOffset.UtcNow,
                ActionRequired: ResolveActionRequired(lifecycleState),
                PrimaryDiagnostic: lifecycleTelemetryState.PrimaryDiagnostic,
                PlayMode: lifecycleTelemetryState.CapturePlayModeSnapshot(
                    isPlaying,
                    isPlayingOrWillChangePlaymode));
        }

        /// <summary> Records one editor update observation. </summary>
        public void ObserveEditorUpdate ()
        {
            lifecycleTelemetryState.ObserveEditorUpdate(
                isPlayingOrWillChangePlaymodeProvider(),
                isCompilingProvider(),
                isUpdatingProvider());
        }

        /// <summary> Records the start of one script compilation cycle. </summary>
        public void OnCompilationStarted ()
        {
            lifecycleTelemetryState.OnCompilationStarted();
        }

        /// <summary> Records compiler diagnostics emitted by one assembly compilation. </summary>
        public void OnAssemblyCompilationFinished (CompilerMessage[] messages)
        {
            lifecycleTelemetryState.OnAssemblyCompilationFinished(messages);
        }

        /// <summary> Records completion of one script compilation cycle. </summary>
        public void OnCompilationFinished ()
        {
            lifecycleTelemetryState.OnCompilationFinished();
        }

        /// <summary> Records the start of one domain reload. </summary>
        public void OnBeforeAssemblyReload ()
        {
            lifecycleTelemetryState.OnBeforeAssemblyReload();
        }

        /// <summary> Records completion of one domain reload. </summary>
        public void OnAfterAssemblyReload ()
        {
            lifecycleTelemetryState.OnAfterAssemblyReload();
        }

        /// <summary> Records that editor shutdown has started. </summary>
        public void OnShutdownStarted ()
        {
            lifecycleTelemetryState.OnShutdownStarted();
        }

        /// <summary> Records one Play Mode state transition callback. </summary>
        public void OnPlayModeStateChanged (UnityEditor.PlayModeStateChange stateChange)
        {
            lifecycleTelemetryState.OnPlayModeStateChanged(stateChange);
        }

        private static string ResolveActionRequired (string lifecycleState)
        {
            return lifecycleState switch
            {
                IpcEditorLifecycleStateCodec.CompileFailed => DaemonDiagnosisActionRequiredValues.FixCompileErrors,
                IpcEditorLifecycleStateCodec.ModalBlocked => DaemonDiagnosisActionRequiredValues.ResolveUnityDialog,
                IpcEditorLifecycleStateCodec.SafeMode => DaemonDiagnosisActionRequiredValues.ResolveUnityDialog,
                IpcEditorLifecycleStateCodec.Unavailable => DaemonDiagnosisActionRequiredValues.InspectUnityLog,
                _ => null,
            };
        }
    }
}
