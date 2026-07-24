
namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines stable dirty-state item kind literals for build probes. </summary>
[VocabularyDefinition]
public enum IpcBuildDirtyStateItemKind
{
    /// <summary> Identifies a Unity scene asset. </summary>
    [VocabularyText("scene")]
    Scene = 1,

    /// <summary> Identifies a Unity prefab asset. </summary>
    [VocabularyText("prefab")]
    Prefab = 2,

    /// <summary> Identifies a Unity asset that is not classified more specifically. </summary>
    [VocabularyText("asset")]
    Asset = 3,

    /// <summary> Identifies a Unity project settings asset. </summary>
    [VocabularyText("projectSettings")]
    ProjectSettings = 4,
}
