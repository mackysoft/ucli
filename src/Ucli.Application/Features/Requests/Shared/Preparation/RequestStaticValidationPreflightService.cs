using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

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
    public async ValueTask<RequestStaticValidationPreflightResult> Prepare (
        PreparedRequestContext preparedRequest,
        ReadIndexMode? readIndexMode,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(preparedRequest);

        var readIndexModeResult = ReadIndexModeResolver.Resolve(readIndexMode, preparedRequest.ProjectContext.Config);
        var validationCatalogResolutionResult = await readIndexValidationCatalogResolver.Resolve(
                preparedRequest.ProjectContext.UnityProject,
                readIndexModeResult.Mode!.Value,
                cancellationToken)
            .ConfigureAwait(false);
        if (!validationCatalogResolutionResult.IsSuccess)
        {
            return RequestStaticValidationPreflightResult.Failure(
                CreateMetadataResolutionError(
                    validationCatalogResolutionResult.ErrorCode!,
                    validationCatalogResolutionResult.ErrorMessage!),
                preparedRequest,
                validationCatalogResolutionResult.ReadIndex,
                validationCatalogResolutionResult.ErrorCode);
        }

        var validationResult = await requestStaticValidator.Validate(
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
        string errorCode,
        string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        if (string.Equals(errorCode, IpcErrorCodes.InvalidArgument, StringComparison.Ordinal))
        {
            return ExecutionError.InvalidArgument(message);
        }

        if (string.Equals(errorCode, ExecutionErrorCodes.IpcTimeout, StringComparison.Ordinal))
        {
            return ExecutionError.Timeout(message);
        }

        return ExecutionError.InternalError(message);
    }
}
