namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Assets;

/// <summary> Represents one asset-search lookup read result. </summary>
internal sealed record AssetSearchLookupReadResult
{
    private AssetSearchLookupReadResult (
        AssetSearchLookupReadOutput? output,
        string message,
        UcliCode? errorCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (output is null)
        {
            ArgumentNullException.ThrowIfNull(errorCode);
        }
        else if (errorCode is not null)
        {
            throw new ArgumentException("Successful read must not contain an error code.", nameof(errorCode));
        }

        Output = output;
        Message = message;
        ErrorCode = errorCode;
    }

    public AssetSearchLookupReadOutput? Output { get; }

    public string Message { get; }

    public UcliCode? ErrorCode { get; }

    /// <summary> Gets a value indicating whether the lookup read succeeded. </summary>
    public bool IsSuccess => Output is not null;

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
        UcliCode errorCode)
    {
        return new AssetSearchLookupReadResult(null, message, errorCode);
    }
}
