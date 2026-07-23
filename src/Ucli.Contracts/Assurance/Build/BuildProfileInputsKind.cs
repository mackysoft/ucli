
namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Defines build profile input-kind literals. </summary>
[VocabularyDefinition]
public enum BuildProfileInputsKind
{
    /// <summary> Uses build inputs declared directly in the profile. </summary>
    [VocabularyText("explicit")]
    Explicit = 1,

    /// <summary> Uses a Unity Build Profile asset selected by project-relative path. </summary>
    [VocabularyText("unityBuildProfile")]
    UnityBuildProfile = 2,
}
