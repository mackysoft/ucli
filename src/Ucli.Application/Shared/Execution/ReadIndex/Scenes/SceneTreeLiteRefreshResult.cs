namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;

/// <summary> Represents one live refresh result for scene-tree-lite lookup data. </summary>
internal sealed record SceneTreeLiteRefreshResult
{
    private SceneTreeLiteRefreshResult (
        SceneTreeLiteSourceSnapshot? snapshot,
        string? fallbackReason,
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
            throw new ArgumentException("Successful refresh must not contain an error code.", nameof(errorCode));
        }

        if (snapshot is null && fallbackReason is not null)
        {
            throw new ArgumentException("Failed refresh must not contain a fallback reason.", nameof(fallbackReason));
        }

        Snapshot = snapshot;
        FallbackReason = fallbackReason;
        Message = message;
        ErrorCode = errorCode;
    }

    public SceneTreeLiteSourceSnapshot? Snapshot { get; }

    public string? FallbackReason { get; }

    public string Message { get; }

    public UcliCode? ErrorCode { get; }

    /// <summary> Gets a value indicating whether the refresh succeeded. </summary>
    public bool IsSuccess => Snapshot is not null;

    /// <summary> Creates a successful refresh result. </summary>
    public static SceneTreeLiteRefreshResult Success (
        SceneTreeLiteSourceSnapshot snapshot,
        string? fallbackReason)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new SceneTreeLiteRefreshResult(snapshot, fallbackReason, "Scene-tree-lite refresh completed.", null);
    }

    /// <summary> Creates a failed refresh result. </summary>
    public static SceneTreeLiteRefreshResult Failure (
        string message,
        UcliCode errorCode)
    {
        return new SceneTreeLiteRefreshResult(null, null, message, errorCode);
    }
}
