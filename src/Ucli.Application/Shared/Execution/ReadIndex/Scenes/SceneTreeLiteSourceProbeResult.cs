namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;

/// <summary> Represents one scene-tree-lite source probe result. </summary>
/// <param name="ErrorMessage"> The validation error message on failure; otherwise <see langword="null" />. </param>
internal sealed record SceneTreeLiteSourceProbeResult (string? ErrorMessage)
{
    /// <summary> Gets a value indicating whether the source probe succeeded. </summary>
    public bool IsSuccess => ErrorMessage is null;

    /// <summary> Creates a successful source probe result. </summary>
    public static SceneTreeLiteSourceProbeResult Success ()
    {
        return new SceneTreeLiteSourceProbeResult(ErrorMessage: null);
    }

    /// <summary> Creates a failed source probe result. </summary>
    public static SceneTreeLiteSourceProbeResult Failure (string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        return new SceneTreeLiteSourceProbeResult(errorMessage);
    }
}
