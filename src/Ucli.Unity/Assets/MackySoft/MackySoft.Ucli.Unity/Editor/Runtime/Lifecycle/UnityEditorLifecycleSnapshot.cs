using System;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Represents one normalized editor lifecycle snapshot used by ping and readiness gates. </summary>
    internal sealed record UnityEditorLifecycleSnapshot (
        DaemonEditorMode EditorMode,
        string LifecycleState,
        string? BlockingReason,
        string CompileState,
        string CompileGeneration,
        string DomainReloadGeneration,
        bool CanAcceptExecutionRequests,
        DateTimeOffset? ObservedAtUtc = null,
        string ActionRequired = null,
        IpcPrimaryDiagnostic PrimaryDiagnostic = null,
        IpcPlayModeSnapshot PlayMode = null,
        string AssetRefreshGeneration = null);
}
