using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Assets;

/// <summary> Represents one live asset lookup snapshot fetch result. </summary>
internal sealed record AssetLookupSnapshotFetchResult (
    IpcIndexAssetsReadResponse? Response,
    string Message,
    UcliErrorCode? ErrorCode)
{
    /// <summary> Gets a value indicating whether fetch succeeded. </summary>
    public bool IsSuccess => Response is not null && ErrorCode is null;

    /// <summary> Creates a successful fetch result. </summary>
    public static AssetLookupSnapshotFetchResult Success (IpcIndexAssetsReadResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return new AssetLookupSnapshotFetchResult(
            Response: response,
            Message: "Asset lookup snapshot read completed.",
            ErrorCode: null);
    }

    /// <summary> Creates a failed fetch result. </summary>
    public static AssetLookupSnapshotFetchResult Failure (
        string message,
        UcliErrorCode errorCode)
    {
        return new AssetLookupSnapshotFetchResult(
            Response: null,
            Message: message,
            ErrorCode: errorCode);
    }
}
