namespace MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;

/// <summary> Represents the result of resolving one read-index backed validation catalog. </summary>
internal sealed record ReadIndexValidationCatalogResolutionResult
{
    private ReadIndexValidationCatalogResolutionResult (
        RequestStaticValidationCatalog catalog,
        ReadIndexInfo readIndex,
        UcliCode? errorCode,
        string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(readIndex);
        if ((errorCode is null) != (errorMessage is null))
        {
            throw new ArgumentException("Failure code and message must either both be present or both be absent.", nameof(errorCode));
        }

        if (errorMessage is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        }

        Catalog = catalog;
        ReadIndex = readIndex;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public RequestStaticValidationCatalog Catalog { get; }

    public ReadIndexInfo ReadIndex { get; }

    public UcliCode? ErrorCode { get; }

    public string? ErrorMessage { get; }

    /// <summary> Gets a value indicating whether metadata resolution succeeded. </summary>
    public bool IsSuccess => ErrorCode is null;

    /// <summary> Creates a successful metadata-resolution result. </summary>
    /// <param name="catalog"> The resolved validation catalog. </param>
    /// <param name="readIndex"> The emitted read-index payload. </param>
    /// <returns> The successful result. </returns>
    public static ReadIndexValidationCatalogResolutionResult Success (
        RequestStaticValidationCatalog catalog,
        ReadIndexInfo readIndex)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(readIndex);
        return new ReadIndexValidationCatalogResolutionResult(catalog, readIndex, null, null);
    }

    /// <summary> Creates a failed metadata-resolution result. </summary>
    /// <param name="readIndex"> The emitted read-index payload. </param>
    /// <param name="errorCode"> The machine-readable failure code. </param>
    /// <param name="errorMessage"> The user-facing failure message. </param>
    /// <returns> The failed result. </returns>
    public static ReadIndexValidationCatalogResolutionResult Failure (
        ReadIndexInfo readIndex,
        UcliCode errorCode,
        string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(readIndex);
        ArgumentNullException.ThrowIfNull(errorCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        return new ReadIndexValidationCatalogResolutionResult(RequestStaticValidationCatalog.Unavailable, readIndex, errorCode, errorMessage);
    }
}
