namespace MackySoft.Ucli.UnityIntegration.Indexing.Scenes.Access;

/// <summary> Represents one scene-tree-lite read result. </summary>
internal sealed record SceneTreeLiteReadResult (
    SceneTreeLiteReadOutput? Output,
    string Message,
    string? ErrorCode)
{
    /// <summary> Gets a value indicating whether the read succeeded. </summary>
    public bool IsSuccess => Output is not null && ErrorCode is null;

    /// <summary> Creates a successful scene-tree-lite read result. </summary>
    public static SceneTreeLiteReadResult Success (
        SceneTreeLiteReadOutput output,
        string message)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new SceneTreeLiteReadResult(output, message, null);
    }

    /// <summary> Creates a failed scene-tree-lite read result. </summary>
    public static SceneTreeLiteReadResult Failure (
        string message,
        string errorCode)
    {
        return new SceneTreeLiteReadResult(null, message, errorCode);
    }
}