using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Hosting.Cli.Requests.Plan.Preflight;

/// <summary> Prepares the base payload required for <c>plan</c> command failures before execution begins. </summary>
internal interface IPlanCommandPreflightService
{
    /// <summary> Prepares one <c>plan</c> command failure context. </summary>
    /// <param name="requestId"> The non-empty correlation identifier owned by the CLI command invocation. </param>
    /// <param name="projectPath"> The optional <c>--projectPath</c> value. </param>
    /// <param name="requestJson"> The raw request JSON read by the CLI host. </param>
    /// <param name="readIndexMode"> The optional normalized <c>--readIndexMode</c> override. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The preflight result containing either the base payload or one normalized failure. </returns>
    ValueTask<PlanCommandPreflightResult> PrepareAsync (
        Guid requestId,
        string? projectPath,
        string requestJson,
        ReadIndexMode? readIndexMode,
        CancellationToken cancellationToken = default);
}
