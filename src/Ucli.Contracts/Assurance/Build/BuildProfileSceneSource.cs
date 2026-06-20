using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Defines build profile scene-source literals. </summary>
public enum BuildProfileSceneSource
{
    /// <summary> Uses enabled Unity Editor Build Settings scenes. </summary>
    [UcliContractLiteral("editorBuildSettings")]
    EditorBuildSettings = 0,

    /// <summary> Uses explicit profile scene paths. </summary>
    [UcliContractLiteral("explicit")]
    Explicit = 1,

    /// <summary> Uses scenes resolved from a Unity Build Profile asset. </summary>
    [UcliContractLiteral("unityBuildProfile")]
    UnityBuildProfile = 2,
}
