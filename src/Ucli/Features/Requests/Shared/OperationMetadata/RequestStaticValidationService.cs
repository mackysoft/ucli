using MackySoft.Ucli.Shared.Context;
using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Features.Requests.Shared.OperationMetadata;

/// <summary> Loads authoritative operation metadata and delegates to the pure static validator. </summary>
internal sealed class RequestStaticValidationService : IRequestStaticValidationService
{
    private readonly IOperationCatalog operationCatalog;

    private readonly IRequestStaticValidator requestStaticValidator;

    /// <summary> Initializes a new instance of the <see cref="RequestStaticValidationService" /> class. </summary>
    /// <param name="operationCatalog"> The authoritative operation-catalog dependency. </param>
    /// <param name="requestStaticValidator"> The pure static-validator dependency. </param>
    public RequestStaticValidationService (
        IOperationCatalog operationCatalog,
        IRequestStaticValidator requestStaticValidator)
    {
        this.operationCatalog = operationCatalog ?? throw new ArgumentNullException(nameof(operationCatalog));
        this.requestStaticValidator = requestStaticValidator ?? throw new ArgumentNullException(nameof(requestStaticValidator));
    }

    /// <inheritdoc />
    public async ValueTask<ValidationResult> Validate (
        ValidateRequest request,
        ProjectContext projectContext,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(projectContext);

        IReadOnlyList<UcliOperationDescriptor> operations;
        try
        {
            operations = await operationCatalog.GetAll(
                    projectContext.UnityProject,
                    projectContext.Config,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCatalogLoadException exception)
        {
            return ValidationResult.Failure(exception.CreatePrefixedError("Static validation could not load operation metadata."));
        }
        catch (InvalidOperationException exception)
        {
            return ValidationResult.Failure(ExecutionError.InternalError(
                $"Static validation could not load operation metadata. {exception.Message}"));
        }

        return await requestStaticValidator.Validate(
                request,
                RequestStaticValidationCatalog.Available(operations),
                projectContext.Config,
                cancellationToken)
            .ConfigureAwait(false);
    }
}