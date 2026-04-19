using MackySoft.Ucli.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;

namespace MackySoft.Ucli.Features.Requests.Call;

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