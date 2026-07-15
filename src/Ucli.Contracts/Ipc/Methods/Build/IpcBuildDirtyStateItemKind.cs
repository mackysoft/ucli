using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines stable dirty-state item kind literals for build probes. </summary>
public enum IpcBuildDirtyStateItemKind
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

    /// <summary> Identifies a dirty item whose kind could not be classified. </summary>
    [UcliContractLiteral("unknown")]
    Unknown = 5,
}
