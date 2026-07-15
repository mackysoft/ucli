using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Scenes;

/// <summary> Represents one scene-tree-lite snapshot fetch result. </summary>
internal sealed record SceneTreeLiteSnapshotFetchResult
{
    private SceneTreeLiteSnapshotFetchResult (
        SceneTreeLiteSourceSnapshot? snapshot,
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
            throw new ArgumentException("Successful fetch must not contain an error code.", nameof(errorCode));
        }

        Snapshot = snapshot;
        Message = message;
        ErrorCode = errorCode;
    }

    public SceneTreeLiteSourceSnapshot? Snapshot { get; }

    public string Message { get; }

    public UcliCode? ErrorCode { get; }

    /// <summary> Gets a value indicating whether the snapshot fetch succeeded. </summary>
    public bool IsSuccess => Snapshot is not null;

    /// <summary> Creates a successful snapshot fetch result. </summary>
    public static SceneTreeLiteSnapshotFetchResult Success (SceneTreeLiteSourceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new SceneTreeLiteSnapshotFetchResult(snapshot, "Scene-tree-lite snapshot read completed.", null);
    }

    /// <summary> Creates a failed snapshot fetch result. </summary>
    public static SceneTreeLiteSnapshotFetchResult Failure (
        string message,
        UcliCode errorCode)
    {
        return new SceneTreeLiteSnapshotFetchResult(null, message, errorCode);
    }
}
