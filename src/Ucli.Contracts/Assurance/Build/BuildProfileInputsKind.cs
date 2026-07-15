using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Defines build profile input-kind literals. </summary>
public enum BuildProfileInputsKind
{
    /// <summary> Uses build inputs declared directly in the profile. </summary>
    [UcliContractLiteral("explicit")]
    Explicit = 1,

    /// <summary> Uses a Unity Build Profile asset selected by project-relative path. </summary>
    [UcliContractLiteral("unityBuildProfile")]
    UnityBuildProfile = 2,
}
