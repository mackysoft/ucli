using System;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Represents one normalized editor lifecycle snapshot used by ping and readiness gates. </summary>
    internal sealed record UnityEditorLifecycleSnapshot (
        DaemonEditorMode EditorMode,
        IpcEditorLifecycleState LifecycleState,
        IpcCompileState CompileState,
        int CompileGeneration,
        int DomainReloadGeneration,
        DateTimeOffset? ObservedAtUtc = null,
        IpcPrimaryDiagnostic PrimaryDiagnostic = null,
        UnityEditorPlayModeSnapshot PlayMode = null,
        int AssetRefreshGeneration = 0)
    {
        /// <summary> Gets the blocking reason derived from the lifecycle state. </summary>
        public IpcEditorBlockingReason? BlockingReason =>
            IpcEditorLifecycleSemantics.ResolveBlockingReason(LifecycleState);

        /// <summary> Gets a value indicating whether normal execution requests may be accepted. </summary>
        public bool CanAcceptExecutionRequests =>
            IpcEditorLifecycleSemantics.CanAcceptExecutionRequests(LifecycleState);

        /// <summary> Gets the action required to resolve the lifecycle state, when one is known. </summary>
        public string ActionRequired => UnityEditorExecutionReadinessPolicy.ResolveActionRequired(LifecycleState);
    }
}
