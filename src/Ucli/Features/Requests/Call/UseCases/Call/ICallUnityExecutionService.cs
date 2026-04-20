using MackySoft.Ucli.Features.Requests.Call.Common.Contracts;
using MackySoft.Ucli.Features.Requests.Shared.Execution.Phase;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Ucli.Features.Requests.Call.UseCases.Call;

/// <summary> Executes the Unity-side portion of the <c>call</c> workflow after preflight succeeds. </summary>
internal interface ICallUnityExecutionService
{
    /// <summary> Executes optional pre-plan and final call IPC passes within one shared timeout budget. </summary>
    ValueTask<CallServiceResult> Execute (
        PhaseExecutionPreparedRequest preparedRequest,
        UnityExecutionMode mode,
        CallCommandInput input,
        ExecutionDeadline deadline,
        CancellationToken cancellationToken = default);
}