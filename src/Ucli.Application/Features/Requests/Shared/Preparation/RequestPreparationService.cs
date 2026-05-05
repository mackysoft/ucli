using MackySoft.Ucli.Application.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Application.Shared.Context;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;

/// <summary> Implements the shared request preparation flow for request-driven commands. </summary>
internal sealed class RequestPreparationService : IRequestPreparationService
{
    private readonly IUserRequestJsonNormalizer userRequestJsonNormalizer;

    private readonly IValidateRequestJsonParser requestJsonParser;

    private readonly IProjectContextResolver projectContextResolver;

    /// <summary> Initializes a new instance of the <see cref="RequestPreparationService" /> class. </summary>
    /// <param name="userRequestJsonNormalizer"> The user request normalizer dependency. </param>
    /// <param name="requestJsonParser"> The request parser dependency. </param>
    /// <param name="projectContextResolver"> The project-context resolver dependency. </param>
    public RequestPreparationService (
        IUserRequestJsonNormalizer userRequestJsonNormalizer,
        IValidateRequestJsonParser requestJsonParser,
        IProjectContextResolver projectContextResolver)
    {
        this.userRequestJsonNormalizer = userRequestJsonNormalizer ?? throw new ArgumentNullException(nameof(userRequestJsonNormalizer));
        this.requestJsonParser = requestJsonParser ?? throw new ArgumentNullException(nameof(requestJsonParser));
        this.projectContextResolver = projectContextResolver ?? throw new ArgumentNullException(nameof(projectContextResolver));
    }

    /// <inheritdoc />
    public ParsedRequestResult Parse (string requestJson)
    {
        var normalizationResult = userRequestJsonNormalizer.Normalize(requestJson);
        if (!normalizationResult.IsSuccess)
        {
            return ParsedRequestResult.Failure(normalizationResult.Error!);
        }

        var normalizedRequestJson = normalizationResult.RequestJson!;
        var parseResult = requestJsonParser.Parse(normalizedRequestJson);
        if (!parseResult.IsSuccess)
        {
            return ParsedRequestResult.Failure(parseResult.Error!);
        }

        return ParsedRequestResult.Success(new ParsedRequestContext(
            RequestJson: normalizedRequestJson,
            Request: parseResult.Request!));
    }

    /// <inheritdoc />
    public async ValueTask<RequestPreparationResult> Prepare (
        string? projectPath,
        string requestJson,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var parseRequestResult = Parse(requestJson);
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
            Request: parsedRequest.Request,
            ProjectContext: projectContextResult.Context!));
    }
}
