using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Application.Features.Requests.Refresh.UseCases.Refresh;

/// <summary> Represents the closed successful output of one refresh Lifecycle Execution. </summary>
internal sealed record RefreshExecutionOutput (
    ProjectIdentityInfo Project,
    Guid RequestId,
    ITerminalExecutionRef LifecycleExecutionRef,
    RefreshLifecycleResult.RefreshEvidence Refresh,
    UnityEditorObservation Lifecycle,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    ExecutionReadPostcondition? ReadPostcondition)
{
    public ITerminalExecutionRef LifecycleExecutionRef { get; } =
        LifecycleExecutionContractGuard.RequireCompletedTerminalReference(
            LifecycleExecutionRef,
            nameof(LifecycleExecutionRef),
            LifecycleExecutionKind.Refresh);
}
