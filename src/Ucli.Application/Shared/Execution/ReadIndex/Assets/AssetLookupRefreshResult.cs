namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Assets;

/// <summary> Represents one refreshed asset lookup snapshot read. </summary>
internal sealed record AssetLookupRefreshResult
{
    private AssetLookupRefreshResult (
        AssetLookupSnapshot? snapshot,
        string message,
        UcliCode? errorCode,
        string? fallbackReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (snapshot is null)
        {
            ArgumentNullException.ThrowIfNull(errorCode);
        }
        else if (errorCode is not null)
        {
            throw new ArgumentException("Successful refresh must not contain an error code.", nameof(errorCode));
        }

        if (snapshot is null && fallbackReason is not null)
        {
            throw new ArgumentException("Failed refresh must not contain a fallback reason.", nameof(fallbackReason));
        }

        Snapshot = snapshot;
        Message = message;
        ErrorCode = errorCode;
        FallbackReason = fallbackReason;
    }

    public AssetLookupSnapshot? Snapshot { get; }

    public string Message { get; }

    public UcliCode? ErrorCode { get; }

    public string? FallbackReason { get; }

    /// <summary> Gets a value indicating whether refresh succeeded. </summary>
    public bool IsSuccess => Snapshot is not null;

    /// <summary> Creates a successful refresh result. </summary>
    public static AssetLookupRefreshResult Success (
        AssetLookupSnapshot snapshot,
        string? fallbackReason)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new AssetLookupRefreshResult(
            snapshot,
            "Asset lookup refresh completed.",
            null,
            fallbackReason);
    }

    /// <summary> Creates a failed refresh result. </summary>
    public static AssetLookupRefreshResult Failure (
        string message,
        UcliCode errorCode)
    {
        return new AssetLookupRefreshResult(
            null,
            message,
            errorCode,
            null);
    }
}
