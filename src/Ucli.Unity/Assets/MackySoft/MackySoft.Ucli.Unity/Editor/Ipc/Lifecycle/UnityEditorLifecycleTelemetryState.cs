using System.Globalization;
using System.Threading;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Stores mutable lifecycle telemetry that is shared across readiness snapshots and Unity callbacks. </summary>
    internal sealed class UnityEditorLifecycleTelemetryState
    {
        private int compileGeneration;

        private int domainReloadGeneration;

        private bool isDomainReloading;

        private bool isShuttingDown;

        private bool isStartupPending;

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
            bool isStartupPending)
        {
            this.compileGeneration = compileGeneration;
            this.domainReloadGeneration = domainReloadGeneration;
            this.isDomainReloading = isDomainReloading;
            this.isShuttingDown = isShuttingDown;
            this.isStartupPending = isStartupPending;
        }

        /// <summary> Gets the current compile-generation counter. </summary>
        public string CompileGeneration => Volatile.Read(ref compileGeneration).ToString(CultureInfo.InvariantCulture);

        /// <summary> Gets the current domain-reload generation counter. </summary>
        public string DomainReloadGeneration => Volatile.Read(ref domainReloadGeneration).ToString(CultureInfo.InvariantCulture);

        /// <summary> Resolves the current lifecycle-state from the tracked editor activity flags. </summary>
        /// <param name="isCompiling"> Whether script compilation is in progress. </param>
        /// <param name="isUpdating"> Whether editor import/update work is in progress. </param>
        /// <returns> The canonical lifecycle-state literal. </returns>
        public string ResolveLifecycleState (
            bool isCompiling,
            bool isUpdating)
        {
            return UnityEditorLifecycleStateResolver.Resolve(
                isStartupPending,
                isShuttingDown,
                isDomainReloading,
                isCompiling,
                isUpdating);
        }

        /// <summary> Advances startup tracking after one editor update confirms no higher-priority blocking state remains. </summary>
        /// <param name="isCompiling"> Whether script compilation is in progress. </param>
        /// <param name="isUpdating"> Whether editor import/update work is in progress. </param>
        internal void ObserveEditorUpdate (
            bool isCompiling,
            bool isUpdating)
        {
            if (!isStartupPending)
            {
                return;
            }

            if (isShuttingDown || isDomainReloading || isCompiling || isUpdating)
            {
                return;
            }

            isStartupPending = false;
        }

        /// <summary> Records the start of one compilation cycle. </summary>
        public void OnCompilationStarted ()
        {
            Interlocked.Increment(ref compileGeneration);
            isStartupPending = true;
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
            Interlocked.Exchange(
                ref domainReloadGeneration,
                UnityEditorDomainReloadGenerationStore.Advance(Volatile.Read(ref domainReloadGeneration)));
        }

        /// <summary> Records the completion of one domain reload. </summary>
        public void OnAfterAssemblyReload ()
        {
            isDomainReloading = false;
            domainReloadGeneration = UnityEditorDomainReloadGenerationStore.Restore();
            isStartupPending = true;
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

    }
}
