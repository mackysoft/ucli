
namespace MackySoft.Ucli.Contracts;

/// <summary> Identifies the persistence-unit kind of a touched Unity resource. </summary>
[VocabularyDefinition]
public enum UcliTouchedResourceKind
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
