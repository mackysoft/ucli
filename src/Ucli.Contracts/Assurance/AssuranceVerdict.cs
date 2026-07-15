using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Defines the finite verdict values emitted by assurance commands. </summary>
public enum AssuranceVerdict
{
    /// <summary> All required claims passed with full coverage. </summary>
    [UcliContractLiteral("pass")]
    Pass = 1,

    /// <summary> A blocking risk or required claim failed. </summary>
    [UcliContractLiteral("fail")]
    Fail = 2,

    /// <summary> A required claim did not reach complete evidence. </summary>
    [UcliContractLiteral("incomplete")]
    Incomplete = 3,
}
