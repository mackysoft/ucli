using System.Globalization;
using System.Threading;
using MackySoft.Ucli.Contracts.Ipc;
using UnityEditor.Compilation;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Stores mutable lifecycle telemetry that is shared across readiness snapshots and Unity callbacks. </summary>
    internal sealed class UnityEditorLifecycleTelemetryState
    {
        private int compileGeneration;

        private int domainReloadGeneration;

        private bool isDomainReloading;

        private bool isShuttingDown;

        private bool isStartupPending;

        private bool isRecoveringPending;

        private bool hasCompileFailure;

        private IpcPrimaryDiagnostic primaryDiagnostic;

        /// <summary> Initializes a new instance of the <see cref="UnityEditorLifecycleTelemetryState" /> class. </summary>
        public UnityEditorLifecycleTelemetryState ()
            : this(
                compileGeneration: 0,
                domainReloadGeneration: UnityEditorDomainReloadGenerationStore.Restore(),
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
        internal UnityEditorLifecycleTelemetryState (
            int compileGeneration,
            int domainReloadGeneration,
            bool isDomainReloading,
            bool isShuttingDown,
            bool isStartupPending,
            bool isRecoveringPending = false,
            bool hasCompileFailure = false,
            IpcPrimaryDiagnostic primaryDiagnostic = null)
        {
            this.compileGeneration = compileGeneration;
            this.domainReloadGeneration = domainReloadGeneration;
            this.isDomainReloading = isDomainReloading;
            this.isShuttingDown = isShuttingDown;
            this.isStartupPending = isStartupPending;
            this.isRecoveringPending = isRecoveringPending;
            this.hasCompileFailure = hasCompileFailure;
            this.primaryDiagnostic = primaryDiagnostic;
        }

        /// <summary> Gets the current compile-generation counter. </summary>
        public string CompileGeneration => Volatile.Read(ref compileGeneration).ToString(CultureInfo.InvariantCulture);

        /// <summary> Gets the current domain-reload generation counter. </summary>
        public string DomainReloadGeneration => Volatile.Read(ref domainReloadGeneration).ToString(CultureInfo.InvariantCulture);

        /// <summary> Gets a value indicating whether the latest completed script compilation failed. </summary>
        public bool HasCompileFailure => hasCompileFailure;

        /// <summary> Gets the primary diagnostic for the latest lifecycle blocker when available. </summary>
        public IpcPrimaryDiagnostic PrimaryDiagnostic => primaryDiagnostic;

        /// <summary> Resolves the current lifecycle-state from the tracked editor activity flags. </summary>
        /// <param name="isPlaymodeActive"> Whether Play Mode is active or about to activate. </param>
        /// <param name="isCompiling"> Whether script compilation is in progress. </param>
        /// <param name="isUpdating"> Whether editor import/update work is in progress. </param>
        /// <returns> The canonical lifecycle-state literal. </returns>
        public string ResolveLifecycleState (
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

        /// <summary> Advances startup tracking after one editor update confirms no higher-priority blocking state remains. </summary>
        /// <param name="isPlaymodeActive"> Whether Play Mode is active or about to activate. </param>
        /// <param name="isCompiling"> Whether script compilation is in progress. </param>
        /// <param name="isUpdating"> Whether editor import/update work is in progress. </param>
        internal void ObserveEditorUpdate (
            bool isPlaymodeActive,
            bool isCompiling,
            bool isUpdating)
        {
            if (isShuttingDown || isPlaymodeActive || isDomainReloading || isCompiling || isUpdating)
            {
                return;
            }

            isStartupPending = false;
            isRecoveringPending = false;
        }

        /// <summary> Records the start of one compilation cycle. </summary>
        public void OnCompilationStarted ()
        {
            Interlocked.Increment(ref compileGeneration);
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
            Interlocked.Increment(ref compileGeneration);
        }

        /// <summary> Records the start of one domain reload. </summary>
        public void OnBeforeAssemblyReload ()
        {
            isDomainReloading = true;
            isStartupPending = true;
            isRecoveringPending = false;
            Interlocked.Exchange(
                ref domainReloadGeneration,
                UnityEditorDomainReloadGenerationStore.Advance(Volatile.Read(ref domainReloadGeneration)));
        }

        /// <summary> Records the completion of one domain reload. </summary>
        public void OnAfterAssemblyReload ()
        {
            isDomainReloading = false;
            domainReloadGeneration = UnityEditorDomainReloadGenerationStore.Restore();
            isStartupPending = false;
            isRecoveringPending = true;
        }

        /// <summary> Records that editor shutdown has started. </summary>
        public void OnShutdownStarted ()
        {
            isShuttingDown = true;
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

        private static IpcPrimaryDiagnostic CreateCompilerDiagnostic (CompilerMessage message)
        {
            return new IpcPrimaryDiagnostic(
                Kind: "compiler",
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
