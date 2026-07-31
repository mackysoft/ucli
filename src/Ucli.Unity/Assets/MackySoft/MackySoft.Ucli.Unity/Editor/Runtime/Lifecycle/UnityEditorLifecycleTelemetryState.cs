using System.Threading;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using UnityEditor;
using UnityEditor.Compilation;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Stores mutable lifecycle telemetry that is shared across readiness snapshots and Unity callbacks. </summary>
    internal sealed class UnityEditorLifecycleTelemetryState
    {
        private long compileGeneration;

        private long domainReloadGeneration;

        private long assetRefreshGeneration;

        private long playModeGeneration;

        private UnityEditorPlayModeTransition playModeTransition;

        private UnityEditorPlayModeState? lastStablePlayModeState;

        private bool isDomainReloading;

        private bool isShuttingDown;

        private bool isStartupPending;

        private bool isRecoveringPending;

        private bool hasCompileFailure;

        private UnityEditorPrimaryDiagnostic primaryDiagnostic;

        /// <summary> Initializes a new instance of the <see cref="UnityEditorLifecycleTelemetryState" /> class. </summary>
        public UnityEditorLifecycleTelemetryState ()
            : this(
                compileGeneration: UnityEditorSessionStateStore.RestoreCompileGeneration(),
                domainReloadGeneration: UnityEditorSessionStateStore.RestoreDomainReloadGeneration(),
                playModeGeneration: UnityEditorSessionStateStore.RestorePlayModeGeneration(),
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: true)
        {
        }

        /// <summary> Initializes a new instance of the <see cref="UnityEditorLifecycleTelemetryState" /> class. </summary>
        /// <param name="compileGeneration"> The initial compile-generation counter. </param>
        /// <param name="domainReloadGeneration"> The initial domain-reload generation counter. </param>
        /// <param name="isDomainReloading"> Whether domain reload is in progress. </param>
        /// <param name="isShuttingDown"> Whether editor shutdown has started. </param>
        /// <param name="isStartupPending"> Whether one startup transition still needs to be reported. </param>
        /// <param name="playModeGeneration"> The initial Play Mode generation counter. </param>
        /// <param name="assetRefreshGeneration"> The initial asset-refresh generation counter. </param>
        internal UnityEditorLifecycleTelemetryState (
            long compileGeneration,
            long domainReloadGeneration,
            bool isDomainReloading,
            bool isShuttingDown,
            bool isStartupPending,
            bool isRecoveringPending = false,
            bool hasCompileFailure = false,
            UnityEditorPrimaryDiagnostic primaryDiagnostic = null,
            long? playModeGeneration = null,
            long? assetRefreshGeneration = null)
        {
            this.compileGeneration = compileGeneration;
            this.domainReloadGeneration = domainReloadGeneration;
            this.assetRefreshGeneration = assetRefreshGeneration ?? UnityEditorSessionStateStore.RestoreAssetRefreshGeneration();
            this.playModeGeneration = playModeGeneration ?? UnityEditorSessionStateStore.RestorePlayModeGeneration();
            playModeTransition = UnityEditorPlayModeTransition.None;
            lastStablePlayModeState = UnityEditorSessionStateStore.RestorePlayModeStableState();
            this.isDomainReloading = isDomainReloading;
            this.isShuttingDown = isShuttingDown;
            this.isStartupPending = isStartupPending;
            this.isRecoveringPending = isRecoveringPending;
            this.hasCompileFailure = hasCompileFailure;
            this.primaryDiagnostic = primaryDiagnostic;
        }

        /// <summary> Gets the current compile-generation counter. </summary>
        public long CompileGeneration => Volatile.Read(ref compileGeneration);

        /// <summary> Gets the current domain-reload generation counter. </summary>
        public long DomainReloadGeneration => Volatile.Read(ref domainReloadGeneration);

        /// <summary> Gets the current asset-refresh generation counter. </summary>
        public long AssetRefreshGeneration => Volatile.Read(ref assetRefreshGeneration);

        /// <summary> Gets the current Play Mode generation counter. </summary>
        public long PlayModeGeneration => Volatile.Read(ref playModeGeneration);

        /// <summary> Gets a value indicating whether the latest completed script compilation failed. </summary>
        public bool HasCompileFailure => hasCompileFailure;

        /// <summary> Gets the primary diagnostic for the latest lifecycle blocker when available. </summary>
        public UnityEditorPrimaryDiagnostic PrimaryDiagnostic => primaryDiagnostic;

        /// <summary> Resolves the current lifecycle-state from the tracked editor activity flags. </summary>
        /// <param name="isPlaymodeActive"> Whether Play Mode is active or about to activate. </param>
        /// <param name="isCompiling"> Whether script compilation is in progress. </param>
        /// <param name="isUpdating"> Whether editor import/update work is in progress. </param>
        /// <returns> The lifecycle state. </returns>
        public UnityEditorLifecycleState ResolveLifecycleState (
            bool isPlaymodeActive,
            bool isCompiling,
            bool isUpdating)
        {
            return UnityEditorLifecycleStateResolver.Resolve(
                isStartupPending,
                isShuttingDown,
                isPlaymodeActive,
                isDomainReloading,
                isCompiling,
                hasCompileFailure,
                isUpdating,
                isRecoveringPending);
        }

        /// <summary> Captures the current Play Mode subsystem snapshot from observed Unity flags. </summary>
        /// <param name="isPlaying"> Whether Unity reports active Play Mode. </param>
        /// <param name="isPlayingOrWillChangePlaymode"> Whether Unity reports active or pending Play Mode. </param>
        /// <returns> The current Play Mode subsystem snapshot. </returns>
        public UnityEditorPlayModeSnapshot CapturePlayModeSnapshot (
            bool isPlaying,
            bool isPlayingOrWillChangePlaymode)
        {
            var transition = playModeTransition;
            var state = ResolvePlayModeState(transition, isPlaying, isPlayingOrWillChangePlaymode);
            if (transition == UnityEditorPlayModeTransition.None && IsStablePlayModeState(state))
            {
                ObserveStablePlayModeState(state, advanceWhenUnknown: false);
            }

            return new UnityEditorPlayModeSnapshot(
                State: state,
                Transition: transition,
                IsPlaying: isPlaying,
                IsPlayingOrWillChangePlaymode: isPlayingOrWillChangePlaymode);
        }

        /// <summary> Captures all lifecycle generations as one observation. </summary>
        public UnityEditorGenerationSnapshot CaptureGenerationSnapshot ()
        {
            return new UnityEditorGenerationSnapshot(
                CompileGeneration: CompileGeneration,
                DomainReloadGeneration: DomainReloadGeneration,
                AssetRefreshGeneration: AssetRefreshGeneration,
                PlayModeGeneration: PlayModeGeneration);
        }

        /// <summary> Advances startup tracking after one editor update confirms no higher-priority blocking state remains. </summary>
        /// <param name="isPlaymodeActive"> Whether Play Mode is active or about to activate. </param>
        /// <param name="isCompiling"> Whether script compilation is in progress. </param>
        /// <param name="isUpdating"> Whether editor import/update work is in progress. </param>
        internal void ObserveEditorUpdate (
            bool isPlaymodeActive,
            bool isCompiling,
            bool isUpdating)
        {
            if (isShuttingDown || isDomainReloading || isCompiling || isUpdating)
            {
                return;
            }

            isStartupPending = false;
            isRecoveringPending = false;
        }

        /// <summary> Records the start of one compilation cycle. </summary>
        public void OnCompilationStarted ()
        {
            Interlocked.Exchange(
                ref compileGeneration,
                UnityEditorSessionStateStore.AdvanceCompileGeneration(Volatile.Read(ref compileGeneration)));
            hasCompileFailure = false;
            primaryDiagnostic = null;
            isStartupPending = true;
        }

        /// <summary> Records compiler diagnostics emitted by one assembly compilation. </summary>
        /// <param name="messages"> The compiler messages emitted by Unity. </param>
        public void OnAssemblyCompilationFinished (CompilerMessage[] messages)
        {
            if (messages == null)
            {
                return;
            }

            foreach (var message in messages)
            {
                if (message.type != CompilerMessageType.Error)
                {
                    continue;
                }

                hasCompileFailure = true;
                primaryDiagnostic ??= CreateCompilerDiagnostic(message);
            }
        }

        /// <summary> Records the end of one compilation cycle. </summary>
        public void OnCompilationFinished ()
        {
            Interlocked.Exchange(
                ref compileGeneration,
                UnityEditorSessionStateStore.AdvanceCompileGeneration(Volatile.Read(ref compileGeneration)));
        }

        /// <summary> Records that Unity completed an asset refresh pass. </summary>
        public void OnAssetRefreshCompleted ()
        {
            Interlocked.Exchange(
                ref assetRefreshGeneration,
                UnityEditorSessionStateStore.AdvanceAssetRefreshGeneration(Volatile.Read(ref assetRefreshGeneration)));
        }

        /// <summary> Records the start of one domain reload. </summary>
        public void OnBeforeAssemblyReload ()
        {
            isDomainReloading = true;
            isStartupPending = true;
            isRecoveringPending = false;
            Interlocked.Exchange(
                ref domainReloadGeneration,
                UnityEditorSessionStateStore.AdvanceDomainReloadGeneration(Volatile.Read(ref domainReloadGeneration)));
        }

        /// <summary> Records the completion of one domain reload. </summary>
        public void OnAfterAssemblyReload ()
        {
            isDomainReloading = false;
            domainReloadGeneration = UnityEditorSessionStateStore.RestoreDomainReloadGeneration();
            isStartupPending = false;
            isRecoveringPending = true;
        }

        /// <summary> Records that editor shutdown has started. </summary>
        public void OnShutdownStarted ()
        {
            isShuttingDown = true;
        }

        /// <summary> Records a Unity Play Mode transition callback. </summary>
        /// <param name="stateChange"> The Unity Play Mode transition callback value. </param>
        public void OnPlayModeStateChanged (PlayModeStateChange stateChange)
        {
            switch (stateChange)
            {
                case PlayModeStateChange.ExitingEditMode:
                    playModeTransition = UnityEditorPlayModeTransition.Entering;
                    break;
                case PlayModeStateChange.EnteredPlayMode:
                    playModeTransition = UnityEditorPlayModeTransition.None;
                    ObserveStablePlayModeState(UnityEditorPlayModeState.Playing, advanceWhenUnknown: true);
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    playModeTransition = UnityEditorPlayModeTransition.Exiting;
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    playModeTransition = UnityEditorPlayModeTransition.None;
                    ObserveStablePlayModeState(UnityEditorPlayModeState.Stopped, advanceWhenUnknown: true);
                    break;
                default:
                    playModeTransition = UnityEditorPlayModeTransition.None;
                    break;
            }
        }

        /// <summary> Overrides the current domain-reload flag. </summary>
        /// <param name="value"> The next domain-reload flag. </param>
        internal void SetDomainReloading (bool value)
        {
            isDomainReloading = value;
        }

        /// <summary> Overrides the current shutdown flag. </summary>
        /// <param name="value"> The next shutdown flag. </param>
        internal void SetShuttingDown (bool value)
        {
            isShuttingDown = value;
        }

        private static UnityEditorPlayModeState ResolvePlayModeState (
            UnityEditorPlayModeTransition transition,
            bool isPlaying,
            bool isPlayingOrWillChangePlaymode)
        {
            return transition switch
            {
                UnityEditorPlayModeTransition.Entering => UnityEditorPlayModeState.Entering,
                UnityEditorPlayModeTransition.Exiting => UnityEditorPlayModeState.Exiting,
                UnityEditorPlayModeTransition.None => ResolveStablePlayModeState(isPlaying, isPlayingOrWillChangePlaymode),
                _ => UnityEditorPlayModeState.Unknown,
            };
        }

        private static UnityEditorPlayModeState ResolveStablePlayModeState (
            bool isPlaying,
            bool isPlayingOrWillChangePlaymode)
        {
            if (isPlaying && isPlayingOrWillChangePlaymode)
            {
                return UnityEditorPlayModeState.Playing;
            }

            if (!isPlaying && !isPlayingOrWillChangePlaymode)
            {
                return UnityEditorPlayModeState.Stopped;
            }

            if (!isPlaying && isPlayingOrWillChangePlaymode)
            {
                return UnityEditorPlayModeState.Entering;
            }

            return UnityEditorPlayModeState.Unknown;
        }

        private static bool IsStablePlayModeState (UnityEditorPlayModeState state)
        {
            return state is UnityEditorPlayModeState.Playing or UnityEditorPlayModeState.Stopped;
        }

        private void ObserveStablePlayModeState (
            UnityEditorPlayModeState state,
            bool advanceWhenUnknown)
        {
            if (!lastStablePlayModeState.HasValue)
            {
                if (advanceWhenUnknown)
                {
                    AdvancePlayModeGeneration();
                }

                StoreStablePlayModeState(state);
                return;
            }

            if (lastStablePlayModeState.Value != state)
            {
                AdvancePlayModeGeneration();
                StoreStablePlayModeState(state);
            }
        }

        private void StoreStablePlayModeState (UnityEditorPlayModeState state)
        {
            lastStablePlayModeState = state;
            UnityEditorSessionStateStore.SetPlayModeStableState(state);
        }

        private void AdvancePlayModeGeneration ()
        {
            Interlocked.Exchange(
                ref playModeGeneration,
                UnityEditorSessionStateStore.AdvancePlayModeGeneration(Volatile.Read(ref playModeGeneration)));
        }

        private static UnityEditorPrimaryDiagnostic CreateCompilerDiagnostic (CompilerMessage message)
        {
            return new UnityEditorPrimaryDiagnostic(
                Kind: UnityEditorPrimaryDiagnosticKind.Compiler,
                Code: TryExtractCompilerCode(message.message),
                File: string.IsNullOrWhiteSpace(message.file) ? null : message.file,
                Line: message.line > 0 ? message.line : null,
                Column: message.column > 0 ? message.column : null,
                Message: string.IsNullOrWhiteSpace(message.message) ? null : message.message.Trim());
        }

        private static string TryExtractCompilerCode (string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return null;
            }

            var index = message.IndexOf("CS", System.StringComparison.Ordinal);
            if (index < 0)
            {
                return null;
            }

            var end = index + 2;
            while (end < message.Length && char.IsDigit(message[end]))
            {
                end++;
            }

            return end == index + 2
                ? null
                : message[index..end];
        }

    }
}
