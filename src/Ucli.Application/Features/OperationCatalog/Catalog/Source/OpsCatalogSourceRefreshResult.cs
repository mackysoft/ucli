namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;

/// <summary> Represents one ops-catalog source refresh result. </summary>
internal sealed record OpsCatalogSourceRefreshResult
{
    private OpsCatalogSourceRefreshResult (
        OpsCatalogSnapshot? snapshot,
        string? fallbackReason,
        string message,
        UcliCode? errorCode,
        StartupFailureDetail? startupFailure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (snapshot is null)
        {
            ArgumentNullException.ThrowIfNull(errorCode);
            if (fallbackReason is not null)
            {
                throw new ArgumentException("Failed source refresh must not contain a fallback reason.", nameof(fallbackReason));
            }
        }
        else
        {
            if (errorCode is not null)
            {
                throw new ArgumentException("Successful source refresh must not contain an error code.", nameof(errorCode));
            }

            if (startupFailure is not null)
            {
                throw new ArgumentException("Successful source refresh must not contain startup failure details.", nameof(startupFailure));
            }
        }

        Snapshot = snapshot;
        FallbackReason = fallbackReason;
        Message = message;
        ErrorCode = errorCode;
        StartupFailure = startupFailure;
    }

    public OpsCatalogSnapshot? Snapshot { get; }

    public string? FallbackReason { get; }

    public string Message { get; }

    public UcliCode? ErrorCode { get; }

    public StartupFailureDetail? StartupFailure { get; }

    /// <summary> Gets a value indicating whether the source refresh succeeded. </summary>
    public bool IsSuccess => Snapshot is not null;

    /// <summary> Creates a successful source refresh result. </summary>
    public static OpsCatalogSourceRefreshResult Success (OpsCatalogSnapshot snapshot, string? fallbackReason)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new OpsCatalogSourceRefreshResult(
            snapshot,
            fallbackReason,
            "Ops catalog refresh completed.",
            null,
            null);
    }

    /// <summary> Creates a failed source refresh result. </summary>
    public static OpsCatalogSourceRefreshResult Failure (
        string message,
        UcliCode errorCode,
        StartupFailureDetail? startupFailure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentNullException.ThrowIfNull(errorCode);

        return new OpsCatalogSourceRefreshResult(null, null, message, errorCode, startupFailure);
    }
}
