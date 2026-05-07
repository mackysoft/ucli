using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;

/// <summary> Represents one live refresh result for scene-tree-lite lookup data. </summary>
internal sealed record SceneTreeLiteRefreshResult (
    IpcIndexSceneTreeLiteReadResponse? Response,
    string? FallbackReason,
    string Message,
    string? ErrorCode)
{
    /// <summary> Gets a value indicating whether the refresh succeeded. </summary>
    public bool IsSuccess => Response is not null && ErrorCode is null;

    /// <summary> Creates a successful refresh result. </summary>
    public static SceneTreeLiteRefreshResult Success (
        IpcIndexSceneTreeLiteReadResponse response,
        string? fallbackReason)
    {
        ArgumentNullException.ThrowIfNull(response);
        return new SceneTreeLiteRefreshResult(response, fallbackReason, "Scene-tree-lite refresh completed.", null);
    }

    /// <summary> Creates a failed refresh result. </summary>
    public static SceneTreeLiteRefreshResult Failure (
        string message,
        string errorCode)
    {
        return new SceneTreeLiteRefreshResult(null, null, message, errorCode);
    }
}
