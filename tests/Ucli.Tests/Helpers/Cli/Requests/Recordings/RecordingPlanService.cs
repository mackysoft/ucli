using MackySoft.Ucli.Application.Features.Requests.Plan.Common.Contracts;
using MackySoft.Ucli.Application.Features.Requests.Plan.UseCases.Plan;

namespace MackySoft.Tests;

internal sealed class RecordingPlanService : RecordingCommandService<PlanCommandInput, PlanServiceResult>, IPlanService
{
    public RecordingPlanService (
        Func<PlanCommandInput, CancellationToken, ValueTask<PlanServiceResult>> handler)
        : base(handler)
    {
    }

    public ValueTask<PlanServiceResult> ExecuteAsync (
        PlanCommandInput input,
        CancellationToken cancellationToken = default)
    {
        return ExecuteRecordedAsync(input, cancellationToken);
    }
}
