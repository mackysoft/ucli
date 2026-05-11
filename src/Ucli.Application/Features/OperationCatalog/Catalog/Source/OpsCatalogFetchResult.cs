namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;

/// <summary> Represents one operation-catalog fetch result. </summary>
/// <param name="Snapshot"> The validated catalog snapshot on success; otherwise <see langword="null" />. </param>
/// <param name="Message"> The user-facing result message. </param>
/// <param name="ErrorCode"> The machine-readable error code on failure; otherwise <see langword="null" />. </param>
/// <param name="StartupFailure"> The structured startup failure detail when source fetch failed before Unity accepted the request. </param>
internal sealed record OpsCatalogFetchResult (
    OpsCatalogSnapshot? Snapshot,
    string Message,
    UcliErrorCode? ErrorCode,
    StartupFailureDetail? StartupFailure = null)
{
    /// <summary> Gets a value indicating whether fetch succeeded. </summary>
    public bool IsSuccess => Snapshot is not null && ErrorCode is null;

    /// <summary> Creates a successful fetch result. </summary>
    /// <param name="snapshot"> The validated catalog snapshot. </param>
    /// <returns> The successful result. </returns>
    public static OpsCatalogFetchResult Success (OpsCatalogSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new OpsCatalogFetchResult(
            Snapshot: snapshot,
            Message: "Ops catalog read completed.",
            ErrorCode: null);
    }

    /// <summary> Creates a failed fetch result. </summary>
    /// <param name="message"> The user-facing failure message. </param>
    /// <param name="errorCode"> The machine-readable failure code. </param>
    /// <returns> The failed result. </returns>
    public static OpsCatalogFetchResult Failure (
        string message,
        UcliErrorCode errorCode,
        StartupFailureDetail? startupFailure = null)
    {
        return new OpsCatalogFetchResult(
            Snapshot: null,
            Message: message,
            ErrorCode: errorCode,
            StartupFailure: startupFailure);
    }
}
