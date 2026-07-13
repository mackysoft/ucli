using System;
using MackySoft.Ucli.Contracts.Daemon;
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
            var isPlaymodeActive = isPlaying || isPlayingOrWillChangePlaymode;
            var lifecycleState = lifecycleTelemetryState.ResolveLifecycleState(isPlaymodeActive, isCompiling, isUpdating);

            return new UnityEditorLifecycleSnapshot(
                EditorMode: editorMode,
                LifecycleState: lifecycleState,
                CompileState: UnityEditorCompileStateResolver.Resolve(
                    isCompiling,
                    lifecycleTelemetryState.HasCompileFailure),
                CompileGeneration: lifecycleTelemetryState.CompileGeneration,
                DomainReloadGeneration: lifecycleTelemetryState.DomainReloadGeneration,
                ObservedAtUtc: DateTimeOffset.UtcNow,
                PrimaryDiagnostic: lifecycleTelemetryState.PrimaryDiagnostic,
                PlayMode: lifecycleTelemetryState.CapturePlayModeSnapshot(
                    isPlaying,
                    isPlayingOrWillChangePlaymode),
                AssetRefreshGeneration: lifecycleTelemetryState.AssetRefreshGeneration);
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

        /// <summary> Records completion of one asset refresh cycle. </summary>
        public void OnAssetRefreshCompleted ()
        {
            lifecycleTelemetryState.OnAssetRefreshCompleted();
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

    }
}
