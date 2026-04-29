using MackySoft.Ucli.Features.Requests.Plan.Common.Contracts;

namespace MackySoft.Ucli.Features.Requests.Plan.UseCases.Plan;

/// <summary> Executes the <c>plan</c> command workflow. </summary>
internal interface IPlanService
{
    /// <summary> Executes one <c>plan</c> workflow and returns the normalized execution result. </summary>
    /// <param name="input"> The normalized command input. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the plan execution result. </returns>
    ValueTask<PlanServiceResult> Execute (
        PlanCommandInput input,
        CancellationToken cancellationToken = default);
}
