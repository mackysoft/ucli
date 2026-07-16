using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts;

/// <summary> Identifies the persistence-unit kind of a touched Unity resource. </summary>
public enum UcliTouchedResourceKind
{
    /// <summary> Identifies a Unity scene asset. </summary>
    [UcliContractLiteral("scene")]
    Scene = 1,

    /// <summary> Identifies a Unity prefab asset. </summary>
    [UcliContractLiteral("prefab")]
    Prefab = 2,

    /// <summary> Identifies a Unity asset that is not classified more specifically. </summary>
    [UcliContractLiteral("asset")]
    Asset = 3,

    /// <summary> Identifies a Unity project settings asset. </summary>
    [UcliContractLiteral("projectSettings")]
    ProjectSettings = 4,
}
