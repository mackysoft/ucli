
namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines scene-tree source-state kinds. </summary>
[VocabularyDefinition]
public enum SceneTreeSourceStateKind
{
    /// <summary> The tree was read from request-local temporary scene state. </summary>
    [VocabularyText("temporaryScene")]
    TemporaryScene = 1,

    /// <summary> The tree was read from a loaded Unity scene. </summary>
    [VocabularyText("loadedScene")]
    LoadedScene = 2,

    /// <summary> The tree was read from a persisted scene asset opened as a preview scene. </summary>
    [VocabularyText("persistedPreview")]
    PersistedPreview = 3,

    /// <summary> The tree was read from a persisted read-index lookup. </summary>
    [VocabularyText("readIndex")]
    ReadIndex = 4,
}
