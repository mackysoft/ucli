namespace MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;

/// <summary> Reads, parses, and optionally binds one request to project context. </summary>
internal interface IRequestPreparationService
{
    /// <summary> Parses one request without resolving project context. </summary>
    /// <param name="requestJson"> The raw request JSON read by the CLI host. </param>
    /// <returns> The parsed-request result. </returns>
    ParsedRequestResult Parse (string requestJson);

    /// <summary> Prepares one request and returns either the bound context or a structured error. </summary>
    /// <param name="projectPath"> The optional Unity project path from <c>--projectPath</c>. </param>
    /// <param name="requestJson"> The raw request JSON read by the CLI host. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The request-preparation result. </returns>
    ValueTask<RequestPreparationResult> PrepareAsync (
        string? projectPath,
        string requestJson,
        CancellationToken cancellationToken = default);
}
