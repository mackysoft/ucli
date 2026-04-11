namespace MackySoft.Ucli.Assets.Access;

/// <summary> Represents one GUID-path lookup read result. </summary>
internal sealed record GuidPathLookupReadResult (
    GuidPathLookupReadOutput? Output,
    string Message,
    string? ErrorCode)
{
    /// <summary> Gets a value indicating whether the lookup read succeeded. </summary>
    public bool IsSuccess => Output is not null && ErrorCode is null;

    /// <summary> Creates a successful lookup-read result. </summary>
    public static GuidPathLookupReadResult Success (
        GuidPathLookupReadOutput output,
        string message)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new GuidPathLookupReadResult(output, message, null);
    }

    /// <summary> Creates a failed lookup-read result. </summary>
    public static GuidPathLookupReadResult Failure (
        string message,
        string errorCode)
    {
        return new GuidPathLookupReadResult(null, message, errorCode);
    }
}