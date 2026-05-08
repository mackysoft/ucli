namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines scene-tree source-state kinds. </summary>
public enum SceneTreeSourceStateKind
{
    /// <summary> No source-state kind is specified. </summary>
    Unspecified = 0,

    /// <summary> The tree was read from request-local temporary scene state. </summary>
    TemporaryScene = 1,

    /// <summary> The tree was read from a loaded Unity scene. </summary>
    LoadedScene = 2,

    /// <summary> The tree was read from a persisted scene asset opened as a preview scene. </summary>
    PersistedPreview = 3,

    /// <summary> The tree was read from a persisted read-index lookup. </summary>
    ReadIndex = 4,
}
