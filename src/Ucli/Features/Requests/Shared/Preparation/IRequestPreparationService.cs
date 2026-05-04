namespace MackySoft.Ucli.Features.Requests.Shared.Preparation;

/// <summary> Reads, parses, and optionally binds one request to project context. </summary>
internal interface IRequestPreparationService
{
    /// <summary> Reads and parses one request without resolving project context. </summary>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The parsed-request result. </returns>
    ValueTask<ParsedRequestResult> ReadAndParse (CancellationToken cancellationToken = default);

    /// <summary> Prepares one request and returns either the bound context or a structured error. </summary>
    /// <param name="projectPath"> The optional Unity project path from <c>--projectPath</c>. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The request-preparation result. </returns>
    ValueTask<RequestPreparationResult> Prepare (
        string? projectPath,
        CancellationToken cancellationToken = default);
}
