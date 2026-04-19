using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Shared.Context;

namespace MackySoft.Ucli.Features.Requests.Shared.Preparation;

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
    public async ValueTask<ParsedRequestResult> ReadAndParse (
        string? requestPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var inputReadResult = await requestInputReader.ReadAsync(requestPath, cancellationToken).ConfigureAwait(false);
        if (!inputReadResult.IsSuccess)
        {
            return ParsedRequestResult.Failure(inputReadResult.Error!);
        }

        var requestJson = inputReadResult.Json!;
        var parseResult = requestJsonParser.Parse(requestJson);
        if (!parseResult.IsSuccess)
        {
            return ParsedRequestResult.Failure(parseResult.Error!);
        }

        return ParsedRequestResult.Success(new ParsedRequestContext(
            RequestJson: requestJson,
            InputSource: inputReadResult.Source!.Value,
            Request: parseResult.Request!));
    }

    /// <inheritdoc />
    public async ValueTask<RequestPreparationResult> Prepare (
        string? requestPath,
        string? projectPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var parseRequestResult = await ReadAndParse(requestPath, cancellationToken).ConfigureAwait(false);
        if (!parseRequestResult.IsSuccess)
        {
            return RequestPreparationResult.Failure(parseRequestResult.Error!);
        }

        var projectContextResult = await projectContextResolver.Resolve(projectPath, cancellationToken).ConfigureAwait(false);
        if (!projectContextResult.IsSuccess)
        {
            return RequestPreparationResult.Failure(projectContextResult.Error!);
        }

        var parsedRequest = parseRequestResult.ParsedRequest!;
        return RequestPreparationResult.Success(new PreparedRequestContext(
            RequestJson: parsedRequest.RequestJson,
            InputSource: parsedRequest.InputSource,
            Request: parsedRequest.Request,
            ProjectContext: projectContextResult.Context!));
    }
}