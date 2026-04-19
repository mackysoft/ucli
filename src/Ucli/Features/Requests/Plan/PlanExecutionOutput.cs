using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Indexing.ReadIndex;

namespace MackySoft.Ucli.Features.Requests.Plan;

/// <summary> Represents the command payload emitted by one <c>plan</c> execution. </summary>
/// <param name="RequestId"> The execute request identifier. </param>
/// <param name="OpResults"> The per-step execution results. </param>
/// <param name="ReadIndex"> The static-preflight read-index metadata. </param>
/// <param name="PlanToken"> The issued plan token when execution succeeded. </param>
internal sealed record PlanExecutionOutput (
    string RequestId,
    IReadOnlyList<IpcExecuteOperationResult> OpResults,
    ReadIndexInfo ReadIndex,
    string? PlanToken);