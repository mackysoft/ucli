using MackySoft.Ucli.Application.Features.Requests.Call.Common.Contracts;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Phase;

namespace MackySoft.Ucli.Application.Features.Requests.Call.UseCases.Call;

/// <summary> Executes the Unity-side portion of the <c>call</c> workflow after preflight succeeds. </summary>
internal interface ICallUnityExecutionService
{
    /// <summary> Executes optional pre-plan and final call IPC passes within one shared timeout budget. </summary>
    ValueTask<CallServiceResult> ExecuteAsync (
        PhaseExecutionPreparedRequest preparedRequest,
        UnityExecutionMode mode,
        CallCommandInput input,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken = default);
}
