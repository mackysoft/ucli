namespace MackySoft.Ucli.Execution;

/// <summary> Reads, parses, and binds one request to project context. </summary>
internal interface IRequestPreparationService
{
    /// <summary> Prepares one request and returns either the bound context or a structured error. </summary>
    /// <param name="requestPath"> The optional request path from <c>--requestPath</c>. </param>
    /// <param name="projectPath"> The optional Unity project path from <c>--projectPath</c>. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The request-preparation result. </returns>
    ValueTask<RequestPreparationResult> Prepare (
        string? requestPath,
        string? projectPath,
        CancellationToken cancellationToken = default);
}