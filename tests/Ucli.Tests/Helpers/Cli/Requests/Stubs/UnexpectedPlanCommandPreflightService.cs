using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Hosting.Cli.Requests.Plan.Preflight;

namespace MackySoft.Tests;

internal sealed class UnexpectedPlanCommandPreflightService : IPlanCommandPreflightService
{
    public ValueTask<PlanCommandPreflightResult> PrepareAsync (
        Guid requestId,
        string? projectPath,
        string requestJson,
        ReadIndexMode? readIndexMode,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Plan command preflight should not be called.");
    }
}
