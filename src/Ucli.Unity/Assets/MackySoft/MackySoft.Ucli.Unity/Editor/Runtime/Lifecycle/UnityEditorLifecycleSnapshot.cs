using MackySoft.Ucli.Contracts.Daemon;

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
        bool CanAcceptExecutionRequests);
}
