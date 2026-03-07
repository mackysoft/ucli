using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Ops;

/// <summary> Represents one live operation-catalog read result. </summary>
/// <param name="Response"> The live catalog response on success; otherwise <see langword="null" />. </param>
/// <param name="Message"> The user-facing result message. </param>
/// <param name="ErrorCode"> The machine-readable error code on failure; otherwise <see langword="null" />. </param>
internal sealed record OpsCatalogLiveReadResult (
    IpcOpsReadResponse? Response,
    string Message,
    string? ErrorCode)
{
    /// <summary> Gets a value indicating whether live read succeeded. </summary>
    public bool IsSuccess => Response is not null && ErrorCode is null;

    /// <summary> Creates a successful live-read result. </summary>
    /// <param name="response"> The live catalog response. </param>
    /// <returns> The successful result. </returns>
    public static OpsCatalogLiveReadResult Success (IpcOpsReadResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return new OpsCatalogLiveReadResult(
            Response: response,
            Message: "Live ops catalog read completed.",
            ErrorCode: null);
    }

    /// <summary> Creates a failed live-read result. </summary>
    /// <param name="message"> The user-facing failure message. </param>
    /// <param name="errorCode"> The machine-readable failure code. </param>
    /// <returns> The failed result. </returns>
    public static OpsCatalogLiveReadResult Failure (
        string message,
        string errorCode)
    {
        return new OpsCatalogLiveReadResult(
            Response: null,
            Message: message,
            ErrorCode: errorCode);
    }
}