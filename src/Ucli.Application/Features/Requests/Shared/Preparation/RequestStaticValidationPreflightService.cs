using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;

/// <summary> Executes shared snapshot-based static-validation preflight for request-driven commands. </summary>
internal sealed class RequestStaticValidationPreflightService : IRequestStaticValidationPreflightService
{
    private readonly IReadIndexValidationCatalogResolver readIndexValidationCatalogResolver;

    private readonly IRequestStaticValidator requestStaticValidator;

    /// <summary> Initializes a new instance of the <see cref="RequestStaticValidationPreflightService" /> class. </summary>
    /// <param name="readIndexValidationCatalogResolver"> The read-index backed validation-catalog resolver dependency. </param>
    /// <param name="requestStaticValidator"> The static-validator dependency. </param>
    public RequestStaticValidationPreflightService (
        IReadIndexValidationCatalogResolver readIndexValidationCatalogResolver,
        IRequestStaticValidator requestStaticValidator)
    {
        this.readIndexValidationCatalogResolver = readIndexValidationCatalogResolver ?? throw new ArgumentNullException(nameof(readIndexValidationCatalogResolver));
        this.requestStaticValidator = requestStaticValidator ?? throw new ArgumentNullException(nameof(requestStaticValidator));
    }

    /// <inheritdoc />
    public async ValueTask<RequestStaticValidationPreflightResult> PrepareAsync (
        PreparedRequestContext preparedRequest,
        ReadIndexMode? readIndexMode,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(preparedRequest);

        var readIndexModeResult = ReadIndexModeResolver.Resolve(readIndexMode, preparedRequest.ProjectContext.Config);
        var validationCatalogResolutionResult = await readIndexValidationCatalogResolver.ResolveAsync(
                preparedRequest.ProjectContext.UnityProject,
                readIndexModeResult.Mode!.Value,
                cancellationToken)
            .ConfigureAwait(false);
        if (!validationCatalogResolutionResult.IsSuccess)
        {
            return RequestStaticValidationPreflightResult.Failure(
                CreateMetadataResolutionError(
                    validationCatalogResolutionResult.ErrorCode!.Value,
                    validationCatalogResolutionResult.ErrorMessage!),
                preparedRequest,
                validationCatalogResolutionResult.ReadIndex,
                validationCatalogResolutionResult.ErrorCode);
        }

        var validationResult = await requestStaticValidator.ValidateAsync(
                preparedRequest.Request,
                validationCatalogResolutionResult.Catalog,
                preparedRequest.ProjectContext.Config,
                cancellationToken)
            .ConfigureAwait(false);
        if (validationResult.Error != null)
        {
            return RequestStaticValidationPreflightResult.Failure(
                validationResult.Error,
                preparedRequest,
                validationCatalogResolutionResult.ReadIndex);
        }

        if (!validationResult.IsValid)
        {
            return RequestStaticValidationPreflightResult.ValidationFailure(
                preparedRequest,
                validationCatalogResolutionResult.ReadIndex,
                validationResult.Errors);
        }

        return RequestStaticValidationPreflightResult.Success(
            preparedRequest,
            validationCatalogResolutionResult.ReadIndex);
    }

    private static ExecutionError CreateMetadataResolutionError (
        UcliCode errorCode,
        string message)
    {
        if (!errorCode.IsValid)
        {
            throw new ArgumentException("Error code must not be empty.", nameof(errorCode));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        if (errorCode == UcliCoreErrorCodes.InvalidArgument)
        {
            return ExecutionError.InvalidArgument(message);
        }

        if (errorCode == ExecutionErrorCodes.IpcTimeout)
        {
            return ExecutionError.Timeout(message);
        }

        return ExecutionError.InternalError(message);
    }
}
