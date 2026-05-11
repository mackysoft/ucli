using MackySoft.Ucli.Application.Shared.Execution;

namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;

/// <summary> Represents one ops-catalog source refresh result. </summary>
internal sealed record OpsCatalogSourceRefreshResult (
    OpsCatalogSnapshot? Snapshot,
    string? FallbackReason,
    string Message,
    UcliErrorCode? ErrorCode,
    StartupFailureDetail? StartupFailure = null)
{
    /// <summary> Gets a value indicating whether the source refresh succeeded. </summary>
    public bool IsSuccess => Snapshot is not null && ErrorCode is null;

    /// <summary> Creates a successful source refresh result. </summary>
    public static OpsCatalogSourceRefreshResult Success (OpsCatalogSnapshot snapshot, string? fallbackReason)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new OpsCatalogSourceRefreshResult(
            snapshot,
            fallbackReason,
            "Ops catalog refresh completed.",
            null);
    }

    /// <summary> Creates a failed source refresh result. </summary>
    public static OpsCatalogSourceRefreshResult Failure (
        string message,
        UcliErrorCode errorCode,
        StartupFailureDetail? startupFailure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (!errorCode.IsValid)
        {
            throw new ArgumentException("Error code must not be empty.", nameof(errorCode));
        }

        return new OpsCatalogSourceRefreshResult(null, null, message, errorCode, startupFailure);
    }
}
