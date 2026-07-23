
namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Defines build profile scene-source literals. </summary>
[VocabularyDefinition]
public enum BuildProfileSceneSource
{
    /// <summary> Uses enabled Unity Editor Build Settings scenes. </summary>
    [VocabularyText("editorBuildSettings")]
    EditorBuildSettings = 1,

    /// <summary> Uses explicit profile scene paths. </summary>
    [VocabularyText("explicit")]
    Explicit = 2,

    /// <summary> Uses scenes resolved from a Unity Build Profile asset. </summary>
    [VocabularyText("unityBuildProfile")]
    UnityBuildProfile = 3,
}
