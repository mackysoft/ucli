using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines stable project mutation audit coverage literals. </summary>
public enum IpcBuildProjectMutationAuditCoverage
{
    /// <summary> All configured project mutation roots were audited. </summary>
    [UcliContractLiteral("full")]
    Full = 1,

    /// <summary> Some configured project mutation roots were audited, but not all entries were covered. </summary>
    [UcliContractLiteral("partial")]
    Partial = 2,

    /// <summary> The project mutation audit could not produce reliable evidence. </summary>
    [UcliContractLiteral("indeterminate")]
    Indeterminate = 3,
}
