using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines scene-tree source-state kinds. </summary>
public enum SceneTreeSourceStateKind
{
    /// <summary> The tree was read from request-local temporary scene state. </summary>
    [UcliContractLiteral("temporaryScene")]
    TemporaryScene = 1,

    /// <summary> The tree was read from a loaded Unity scene. </summary>
    [UcliContractLiteral("loadedScene")]
    LoadedScene = 2,

    /// <summary> The tree was read from a persisted scene asset opened as a preview scene. </summary>
    [UcliContractLiteral("persistedPreview")]
    PersistedPreview = 3,

    /// <summary> The tree was read from a persisted read-index lookup. </summary>
    [UcliContractLiteral("readIndex")]
    ReadIndex = 4,
}
