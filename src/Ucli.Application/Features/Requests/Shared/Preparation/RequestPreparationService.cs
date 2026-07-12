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
    public async ValueTask<RequestPreparationResult> PrepareAsync (
        string? projectPath,
        string requestJson,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizationResult = userRequestJsonNormalizer.Normalize(requestJson);
        if (!normalizationResult.IsSuccess)
        {
            return RequestPreparationResult.Failure(normalizationResult.Error!);
        }

        var normalizedRequestJson = normalizationResult.RequestJson!;
        var parseResult = requestJsonParser.Parse(normalizedRequestJson);
        if (!parseResult.IsSuccess)
        {
            return RequestPreparationResult.Failure(parseResult.Error!);
        }

        var projectContextResult = await projectContextResolver.ResolveAsync(projectPath, cancellationToken).ConfigureAwait(false);
        if (!projectContextResult.IsSuccess)
        {
            return RequestPreparationResult.Failure(projectContextResult.Error!);
        }

        return RequestPreparationResult.Success(new PreparedRequestContext(
            requestJson: normalizedRequestJson,
            request: parseResult.Request!,
            projectContext: projectContextResult.Context!));
    }
}
