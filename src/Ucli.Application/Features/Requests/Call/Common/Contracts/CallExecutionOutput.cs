using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;

namespace MackySoft.Ucli.Application.Features.Requests.Call.Common.Contracts;

/// <summary> Represents the command payload emitted by one <c>call</c> execution. </summary>
/// <param name="RequestId"> The execute request identifier. </param>
/// <param name="OpResults"> The per-step execution results. </param>
/// <param name="Plan"> The optional plan-equivalent payload returned by <c>--withPlan</c>. </param>
internal sealed record CallExecutionOutput (
    string RequestId,
    IReadOnlyList<OperationExecutionOperationResult> OpResults,
    CallPlanOutput? Plan,
    OperationExecutionReadPostcondition? ReadPostcondition);
