namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines scene-tree source-state kind values. </summary>
public static class SceneTreeSourceStateKindValues
{
    /// <summary> The tree was read from request-local temporary scene state. </summary>
    public const string TemporaryScene = "temporaryScene";

    /// <summary> The tree was read from a loaded Unity scene. </summary>
    public const string LoadedScene = "loadedScene";

    /// <summary> The tree was read from a persisted scene asset opened as a preview scene. </summary>
    public const string PersistedPreview = "persistedPreview";

    /// <summary> The tree was read from a persisted read-index lookup. </summary>
    public const string ReadIndex = "readIndex";
}
