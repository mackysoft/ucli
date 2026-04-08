using MackySoft.Ucli.Cli.Requests;
using MackySoft.Ucli.Context;

namespace MackySoft.Ucli.Execution;

/// <summary> Implements the shared request preparation flow for request-driven commands. </summary>
internal sealed class RequestPreparationService : IRequestPreparationService
{
    private readonly IRequestInputReader requestInputReader;

    private readonly IValidateRequestJsonParser requestJsonParser;

    private readonly IProjectContextResolver projectContextResolver;

    /// <summary> Initializes a new instance of the <see cref="RequestPreparationService" /> class. </summary>
    /// <param name="requestInputReader"> The request-input reader dependency. </param>
    /// <param name="requestJsonParser"> The request parser dependency. </param>
    /// <param name="projectContextResolver"> The project-context resolver dependency. </param>
    public RequestPreparationService (
        IRequestInputReader requestInputReader,
        IValidateRequestJsonParser requestJsonParser,
        IProjectContextResolver projectContextResolver)
    {
        this.requestInputReader = requestInputReader ?? throw new ArgumentNullException(nameof(requestInputReader));
        this.requestJsonParser = requestJsonParser ?? throw new ArgumentNullException(nameof(requestJsonParser));
        this.projectContextResolver = projectContextResolver ?? throw new ArgumentNullException(nameof(projectContextResolver));
    }

    /// <inheritdoc />
    public async ValueTask<RequestPreparationResult> Prepare (
        string? requestPath,
        string? projectPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var inputReadResult = await requestInputReader.ReadAsync(requestPath, cancellationToken).ConfigureAwait(false);
        if (!inputReadResult.IsSuccess)
        {
            return RequestPreparationResult.Failure(inputReadResult.Error!);
        }

        var requestJson = inputReadResult.Json!;
        var parseResult = requestJsonParser.Parse(requestJson);
        if (!parseResult.IsSuccess)
        {
            return RequestPreparationResult.Failure(parseResult.Error!);
        }

        var projectContextResult = await projectContextResolver.Resolve(projectPath, cancellationToken).ConfigureAwait(false);
        if (!projectContextResult.IsSuccess)
        {
            return RequestPreparationResult.Failure(projectContextResult.Error!);
        }

        return RequestPreparationResult.Success(new PreparedRequestContext(
            RequestJson: requestJson,
            InputSource: inputReadResult.Source!.Value,
            Request: parseResult.Request!,
            ProjectContext: projectContextResult.Context!));
    }
}