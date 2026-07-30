using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;

/// <summary> Represents the result of executing shared static-validation preflight for one request. </summary>
internal sealed record RequestStaticValidationPreflightResult
{
    private RequestStaticValidationPreflightResult (
        PreparedRequestContext preparedRequest,
        ReadIndexInfo readIndex,
        RequestStaticValidationCatalog catalog,
        IReadOnlyList<ValidationError> validationErrors,
        ExecutionError? error)
    {
        ArgumentNullException.ThrowIfNull(preparedRequest);
        ArgumentNullException.ThrowIfNull(readIndex);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(validationErrors);
        if (error is not null)
        {
            if (validationErrors.Count != 0)
            {
                throw new ArgumentException("Infrastructure-failure preflight must not contain validation errors.", nameof(validationErrors));
            }
        }

        PreparedRequest = preparedRequest;
        ReadIndex = readIndex;
        Catalog = catalog;
        ValidationErrors = validationErrors;
        Error = error;
    }

    public PreparedRequestContext PreparedRequest { get; }

    public ReadIndexInfo ReadIndex { get; }

    /// <summary> Gets the exact operation catalog used by static validation. </summary>
    public RequestStaticValidationCatalog Catalog { get; }

    public IReadOnlyList<ValidationError> ValidationErrors { get; }

    public ExecutionError? Error { get; }

    /// <summary> Gets a value indicating whether static-validation preflight succeeded. </summary>
    public bool IsSuccess => ValidationErrors.Count == 0 && Error is null;

    /// <summary> Gets a value indicating whether static-validation preflight failed due to validation errors. </summary>
    public bool HasValidationErrors => ValidationErrors.Count > 0;

    /// <summary> Creates a successful static-validation preflight result. </summary>
    /// <param name="preparedRequest"> The prepared request context. </param>
    /// <param name="readIndex"> The emitted read-index payload. </param>
    /// <param name="catalog"> The exact operation catalog used by static validation. </param>
    /// <returns> The successful result. </returns>
    public static RequestStaticValidationPreflightResult Success (
        PreparedRequestContext preparedRequest,
        ReadIndexInfo readIndex,
        RequestStaticValidationCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(preparedRequest);
        ArgumentNullException.ThrowIfNull(readIndex);
        ArgumentNullException.ThrowIfNull(catalog);
        return new RequestStaticValidationPreflightResult(
            preparedRequest,
            readIndex,
            catalog,
            Array.Empty<ValidationError>(),
            error: null);
    }

    /// <summary> Creates a validation-failure result. </summary>
    /// <param name="preparedRequest"> The prepared request context. </param>
    /// <param name="readIndex"> The emitted read-index payload. </param>
    /// <param name="catalog"> The exact operation catalog used by static validation. </param>
    /// <param name="validationErrors"> The validation errors. </param>
    /// <returns> The validation-failure result. </returns>
    public static RequestStaticValidationPreflightResult ValidationFailure (
        PreparedRequestContext preparedRequest,
        ReadIndexInfo readIndex,
        RequestStaticValidationCatalog catalog,
        IReadOnlyList<ValidationError> validationErrors)
    {
        ArgumentNullException.ThrowIfNull(preparedRequest);
        ArgumentNullException.ThrowIfNull(readIndex);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(validationErrors);
        if (validationErrors.Count == 0)
        {
            throw new ArgumentException("Validation errors must not be empty.", nameof(validationErrors));
        }

        return new RequestStaticValidationPreflightResult(
            preparedRequest,
            readIndex,
            catalog,
            validationErrors,
            error: null);
    }

    /// <summary> Creates an infrastructure failure result. </summary>
    /// <param name="error"> The infrastructure error. </param>
    /// <param name="preparedRequest"> The prepared request context. </param>
    /// <param name="readIndex"> The emitted read-index payload. </param>
    /// <param name="catalog"> The operation catalog resolved before the failure. </param>
    /// <returns> The infrastructure-failure result. </returns>
    public static RequestStaticValidationPreflightResult Failure (
        ExecutionError error,
        PreparedRequestContext preparedRequest,
        ReadIndexInfo readIndex,
        RequestStaticValidationCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(preparedRequest);
        ArgumentNullException.ThrowIfNull(readIndex);
        ArgumentNullException.ThrowIfNull(catalog);
        return new RequestStaticValidationPreflightResult(
            preparedRequest,
            readIndex,
            catalog,
            Array.Empty<ValidationError>(),
            error);
    }
}
