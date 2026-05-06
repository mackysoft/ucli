using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Assets;

/// <summary> Represents one refreshed asset lookup snapshot read. </summary>
internal sealed record AssetLookupRefreshResult (
    IpcIndexAssetsReadResponse? Response,
    string Message,
    UcliErrorCode? ErrorCode,
    string? FallbackReason)
{
    /// <summary> Gets a value indicating whether refresh succeeded. </summary>
    public bool IsSuccess => Response is not null && ErrorCode is null;

    /// <summary> Creates a successful refresh result. </summary>
    public static AssetLookupRefreshResult Success (
        IpcIndexAssetsReadResponse response,
        string? fallbackReason)
    {
        ArgumentNullException.ThrowIfNull(response);
        return new AssetLookupRefreshResult(
            Response: response,
            Message: "Asset lookup refresh completed.",
            ErrorCode: null,
            FallbackReason: fallbackReason);
    }

    /// <summary> Creates a failed refresh result. </summary>
    public static AssetLookupRefreshResult Failure (
        string message,
        UcliErrorCode errorCode)
    {
        return new AssetLookupRefreshResult(
            Response: null,
            Message: message,
            ErrorCode: errorCode,
            FallbackReason: null);
    }
}
