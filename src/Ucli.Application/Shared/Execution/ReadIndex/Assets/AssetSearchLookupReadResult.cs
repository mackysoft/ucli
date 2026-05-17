namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Assets;

/// <summary> Represents one asset-search lookup read result. </summary>
internal sealed record AssetSearchLookupReadResult (
    AssetSearchLookupReadOutput? Output,
    string Message,
    UcliCodeValue? ErrorCode)
{
    /// <summary> Gets a value indicating whether the lookup read succeeded. </summary>
    public bool IsSuccess => Output is not null && ErrorCode is null;

    /// <summary> Creates a successful lookup-read result. </summary>
    public static AssetSearchLookupReadResult Success (
        AssetSearchLookupReadOutput output,
        string message)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new AssetSearchLookupReadResult(output, message, null);
    }

    /// <summary> Creates a failed lookup-read result. </summary>
    public static AssetSearchLookupReadResult Failure (
        string message,
        UcliCodeValue errorCode)
    {
        return new AssetSearchLookupReadResult(null, message, errorCode);
    }
}
