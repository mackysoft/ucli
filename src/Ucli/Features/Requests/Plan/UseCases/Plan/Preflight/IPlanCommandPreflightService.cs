using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Features.Requests.Plan.UseCases.Plan.Preflight;

/// <summary> Prepares the base payload required for <c>plan</c> command failures before execution begins. </summary>
internal interface IPlanCommandPreflightService
{
    /// <summary> Prepares one <c>plan</c> command failure context. </summary>
    /// <param name="projectPath"> The optional <c>--projectPath</c> value. </param>
    /// <param name="readIndexMode"> The optional normalized <c>--readIndexMode</c> override. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The preflight result containing either the base payload or one normalized failure. </returns>
    ValueTask<PlanCommandPreflightResult> Prepare (
        string? projectPath,
        ReadIndexMode? readIndexMode,
        CancellationToken cancellationToken = default);
}
