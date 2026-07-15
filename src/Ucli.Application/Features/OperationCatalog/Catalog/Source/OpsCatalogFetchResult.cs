namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;

/// <summary> Represents one operation-catalog fetch result. </summary>
internal sealed record OpsCatalogFetchResult
{
    private OpsCatalogFetchResult (
        OpsCatalogSnapshot? snapshot,
        string message,
        UcliCode? errorCode,
        StartupFailureDetail? startupFailure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (snapshot is null)
        {
            ArgumentNullException.ThrowIfNull(errorCode);
        }
        else
        {
            if (errorCode is not null)
            {
                throw new ArgumentException("Successful fetch result must not contain an error code.", nameof(errorCode));
            }

            if (startupFailure is not null)
            {
                throw new ArgumentException("Successful fetch result must not contain startup failure details.", nameof(startupFailure));
            }
        }

        Snapshot = snapshot;
        Message = message;
        ErrorCode = errorCode;
        StartupFailure = startupFailure;
    }

    public OpsCatalogSnapshot? Snapshot { get; }

    public string Message { get; }

    public UcliCode? ErrorCode { get; }

    public StartupFailureDetail? StartupFailure { get; }

    /// <summary> Gets a value indicating whether fetch succeeded. </summary>
    public bool IsSuccess => Snapshot is not null;

    /// <summary> Creates a successful fetch result. </summary>
    /// <param name="snapshot"> The validated catalog snapshot. </param>
    /// <returns> The successful result. </returns>
    public static OpsCatalogFetchResult Success (OpsCatalogSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new OpsCatalogFetchResult(
            snapshot,
            "Ops catalog read completed.",
            errorCode: null,
            startupFailure: null);
    }

    /// <summary> Creates a failed fetch result. </summary>
    /// <param name="message"> The user-facing failure message. </param>
    /// <param name="errorCode"> The machine-readable failure code. </param>
    /// <returns> The failed result. </returns>
    public static OpsCatalogFetchResult Failure (
        string message,
        UcliCode errorCode,
        StartupFailureDetail? startupFailure = null)
    {
        return new OpsCatalogFetchResult(
            snapshot: null,
            message,
            errorCode,
            startupFailure);
    }
}
