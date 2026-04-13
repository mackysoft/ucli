using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Scenes;

/// <summary> Represents one live scene-tree-lite snapshot fetch result. </summary>
internal sealed record SceneTreeLiteSnapshotFetchResult (
    IpcIndexSceneTreeLiteReadResponse? Response,
    string Message,
    string? ErrorCode)
{
    /// <summary> Gets a value indicating whether the snapshot fetch succeeded. </summary>
    public bool IsSuccess => Response is not null && ErrorCode is null;

    /// <summary> Creates a successful snapshot fetch result. </summary>
    public static SceneTreeLiteSnapshotFetchResult Success (IpcIndexSceneTreeLiteReadResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return new SceneTreeLiteSnapshotFetchResult(response, "Scene-tree-lite snapshot read completed.", null);
    }

    /// <summary> Creates a failed snapshot fetch result. </summary>
    public static SceneTreeLiteSnapshotFetchResult Failure (
        string message,
        string errorCode)
    {
        return new SceneTreeLiteSnapshotFetchResult(null, message, errorCode);
    }
}