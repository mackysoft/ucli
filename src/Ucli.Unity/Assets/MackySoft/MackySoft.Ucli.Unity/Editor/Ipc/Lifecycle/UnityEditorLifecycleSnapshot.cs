namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Represents one normalized editor lifecycle snapshot used by ping and readiness gates. </summary>
    internal sealed record UnityEditorLifecycleSnapshot (
        string Runtime,
        string LifecycleState,
        string? BlockingReason,
        string CompileState,
        string CompileGeneration,
        string DomainReloadGeneration,
        bool CanAcceptExecutionRequests);
}
