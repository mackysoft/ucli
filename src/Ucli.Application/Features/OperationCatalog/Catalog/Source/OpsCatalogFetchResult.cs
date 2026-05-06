using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;

/// <summary> Represents one operation-catalog fetch result. </summary>
/// <param name="Response"> The catalog response on success; otherwise <see langword="null" />. </param>
/// <param name="Message"> The user-facing result message. </param>
/// <param name="ErrorCode"> The machine-readable error code on failure; otherwise <see langword="null" />. </param>
internal sealed record OpsCatalogFetchResult (
    IpcOpsReadResponse? Response,
    string Message,
    UcliErrorCode? ErrorCode)
{
    /// <summary> Gets a value indicating whether fetch succeeded. </summary>
    public bool IsSuccess => Response is not null && ErrorCode is null;

    /// <summary> Creates a successful fetch result. </summary>
    /// <param name="response"> The catalog response. </param>
    /// <returns> The successful result. </returns>
    public static OpsCatalogFetchResult Success (IpcOpsReadResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return new OpsCatalogFetchResult(
            Response: response,
            Message: "Ops catalog read completed.",
            ErrorCode: null);
    }

    /// <summary> Creates a failed fetch result. </summary>
    /// <param name="message"> The user-facing failure message. </param>
    /// <param name="errorCode"> The machine-readable failure code. </param>
    /// <returns> The failed result. </returns>
    public static OpsCatalogFetchResult Failure (
        string message,
        UcliErrorCode errorCode)
    {
        return new OpsCatalogFetchResult(
            Response: null,
            Message: message,
            ErrorCode: errorCode);
    }
}
