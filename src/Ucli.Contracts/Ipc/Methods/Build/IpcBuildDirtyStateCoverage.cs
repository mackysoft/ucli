using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines stable dirty-state coverage literals for build probes. </summary>
public enum IpcBuildDirtyStateCoverage
{
    /// <summary> All configured dirty-state sources were checked. </summary>
    [UcliContractLiteral("full")]
    Full = 0,

    /// <summary> At least one dirty-state source could not be checked completely. </summary>
    [UcliContractLiteral("partial")]
    Partial = 1,
}
