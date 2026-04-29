namespace MackySoft.Ucli.Features.Requests.Shared.OperationMetadata;

/// <summary> Represents the result of resolving one read-index backed validation catalog. </summary>
/// <param name="Catalog"> The resolved validation catalog. </param>
/// <param name="ReadIndex"> The emitted <c>payload.readIndex</c> metadata. </param>
/// <param name="ErrorCode"> The machine-readable failure code when metadata resolution failed; otherwise <see langword="null" />. </param>
/// <param name="ErrorMessage"> The user-facing failure message when metadata resolution failed; otherwise <see langword="null" />. </param>
internal sealed record ReadIndexValidationCatalogResolutionResult (
    RequestStaticValidationCatalog Catalog,
    ReadIndexInfo ReadIndex,
    string? ErrorCode,
    string? ErrorMessage)
{
    /// <summary> Gets a value indicating whether metadata resolution succeeded. </summary>
    public bool IsSuccess => ErrorCode is null && ErrorMessage is null;

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
        string errorCode,
        string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(readIndex);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        return new ReadIndexValidationCatalogResolutionResult(RequestStaticValidationCatalog.Unavailable, readIndex, errorCode, errorMessage);
    }
}
