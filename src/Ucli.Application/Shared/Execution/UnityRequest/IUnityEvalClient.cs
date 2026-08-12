using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Shared.Execution.UnityRequest;

/// <summary> Executes the complete dedicated eval protocol on one selected Unity host. </summary>
internal interface IUnityEvalClient
{
    ValueTask<UnityEvalExecutionResult> ExecuteAsync (
        MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision.UnityExecutionMode mode,
        TimeSpan timeout,
        ResolvedUnityProjectContext unityProject,
        IpcEvalPlanRequest planRequest,
        bool failFast,
        CancellationToken cancellationToken = default);
}

/// <summary> Carries the closed outcomes of one eval.plan/eval.call exchange. </summary>
internal sealed record UnityEvalExecutionResult (
    IpcEvalResponse? Plan,
    IpcEvalResponse? Call,
    IpcEvalErrorResponse? ErrorResponse,
    ExecutionError? Error,
    bool CallWasSent)
{
    public bool IsSuccess => Call is not null && Error is null;

    public static UnityEvalExecutionResult PlanFailure (ExecutionError error, IpcEvalErrorResponse? errorResponse = null) => new(null, null, errorResponse, error, false);

    public static UnityEvalExecutionResult CallFailure (IpcEvalResponse plan, ExecutionError error, bool callWasSent, IpcEvalErrorResponse? errorResponse = null) => new(plan, null, errorResponse, error, callWasSent);

    public static UnityEvalExecutionResult Success (IpcEvalResponse plan, IpcEvalResponse call) => new(plan, call, null, null, true);
}
