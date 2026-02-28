namespace MackySoft.Ucli.Execution;

/// <summary> Executes request preflight for phase-based command execution. </summary>
internal interface IPhaseExecutionPreflightService
{
    /// <summary> Executes preflight and returns a prepared request or structured errors. </summary>
    /// <param name="requestPath"> The optional request path from <c>--requestPath</c>. </param>
    /// <param name="projectPath"> The optional Unity project path from <c>--projectPath</c>. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The preflight result. </returns>
    ValueTask<PhaseExecutionPreflightResult> Prepare (
        string? requestPath,
        string? projectPath,
        CancellationToken cancellationToken = default);
}