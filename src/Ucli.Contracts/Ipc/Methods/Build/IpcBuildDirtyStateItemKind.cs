using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines stable dirty-state item kind literals for build probes. </summary>
public enum IpcBuildDirtyStateItemKind
{
    /// <summary> Identifies a Unity scene asset. </summary>
    [UcliContractLiteral("scene")]
    Scene = 0,
}
