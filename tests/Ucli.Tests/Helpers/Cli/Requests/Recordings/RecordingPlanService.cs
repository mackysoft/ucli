using MackySoft.Ucli.Application.Features.Requests.Plan.Common.Contracts;
using MackySoft.Ucli.Application.Features.Requests.Plan.UseCases.Plan;

namespace MackySoft.Tests;

internal sealed class RecordingPlanService : RecordingCommandService<PlanCommandInput, PlanServiceResult>, IPlanService
{
    private readonly List<Guid> requestIds = [];

    public RecordingPlanService (
        Func<PlanCommandInput, CancellationToken, ValueTask<PlanServiceResult>> handler)
        : base(handler)
    {
    }

    public IReadOnlyList<Guid> RequestIds => requestIds;

    public ValueTask<PlanServiceResult> ExecuteAsync (
        Guid requestId,
        PlanCommandInput input,
        CancellationToken cancellationToken = default)
    {
        requestIds.Add(requestId);
        return ExecuteRecordedAsync(input, cancellationToken);
    }
}
