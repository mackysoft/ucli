namespace MackySoft.Ucli.Features.Requests.Call.UseCases.Call.Preflight;

/// <summary> Prepares the base payload required for <c>call</c> command failures before execution begins. </summary>
internal interface ICallCommandPreflightService
{
    /// <summary> Prepares one <c>call</c> command failure context. </summary>
    /// <param name="requestPath"> The optional <c>--requestPath</c> value. </param>
    /// <param name="projectPath"> The optional <c>--projectPath</c> value. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The preflight result containing either the base payload or one normalized failure. </returns>
    ValueTask<CallCommandPreflightResult> Prepare (
        string? requestPath,
        string? projectPath,
        CancellationToken cancellationToken = default);
}
