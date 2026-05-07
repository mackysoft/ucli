namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;

/// <summary> Represents one ops-catalog source refresh result. </summary>
internal sealed record OpsCatalogSourceRefreshResult (
    IReadOnlyList<IndexOpEntryJsonContract>? Operations,
    DateTimeOffset? GeneratedAtUtc,
    string? FallbackReason,
    string Message,
    string? ErrorCode)
{
    /// <summary> Gets a value indicating whether the source refresh succeeded. </summary>
    public bool IsSuccess => Operations is not null && GeneratedAtUtc.HasValue && ErrorCode is null;

    /// <summary> Creates a successful source refresh result. </summary>
    public static OpsCatalogSourceRefreshResult Success (
        IReadOnlyList<IndexOpEntryJsonContract> operations,
        DateTimeOffset generatedAtUtc,
        string? fallbackReason)
    {
        ArgumentNullException.ThrowIfNull(operations);
        return new OpsCatalogSourceRefreshResult(
            operations,
            generatedAtUtc,
            fallbackReason,
            "Ops catalog refresh completed.",
            null);
    }

    /// <summary> Creates a failed source refresh result. </summary>
    public static OpsCatalogSourceRefreshResult Failure (
        string message,
        string errorCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        return new OpsCatalogSourceRefreshResult(null, null, null, message, errorCode);
    }
}
