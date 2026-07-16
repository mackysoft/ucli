using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Assets;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Assets;

/// <summary> Represents one live asset lookup snapshot fetch result. </summary>
internal sealed record AssetLookupSnapshotFetchResult
{
    private AssetLookupSnapshotFetchResult (
        AssetLookupSnapshot? snapshot,
        string message,
        UcliCode? errorCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (snapshot is null)
        {
            ArgumentNullException.ThrowIfNull(errorCode);
        }
        else if (errorCode is not null)
        {
            throw new ArgumentException("Successful fetch must not contain an error code.", nameof(errorCode));
        }

        Snapshot = snapshot;
        Message = message;
        ErrorCode = errorCode;
    }

    public AssetLookupSnapshot? Snapshot { get; }

    public string Message { get; }

    public UcliCode? ErrorCode { get; }

    /// <summary> Gets a value indicating whether fetch succeeded. </summary>
    public bool IsSuccess => Snapshot is not null;

    /// <summary> Creates a successful fetch result. </summary>
    public static AssetLookupSnapshotFetchResult Success (AssetLookupSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new AssetLookupSnapshotFetchResult(
            snapshot,
            "Asset lookup snapshot read completed.",
            null);
    }

    /// <summary> Creates a failed fetch result. </summary>
    public static AssetLookupSnapshotFetchResult Failure (
        string message,
        UcliCode errorCode)
    {
        return new AssetLookupSnapshotFetchResult(
            null,
            message,
            errorCode);
    }
}
