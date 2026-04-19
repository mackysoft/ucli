using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Features.Requests.Call;

/// <summary> Represents the optional nested plan payload emitted by one <c>call --withPlan</c> execution. </summary>
/// <param name="RequestId"> The execute request identifier shared with the surrounding call execution. </param>
/// <param name="OpResults"> The per-step plan execution results. </param>
/// <param name="PlanToken"> The optional plan token issued by the pre-plan pass. </param>
internal sealed record CallPlanOutput (
    string RequestId,
    IReadOnlyList<IpcExecuteOperationResult> OpResults,
    string? PlanToken);