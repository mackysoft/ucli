using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Identifies the completeness of C# eval touched-resource declarations. </summary>
public enum CsEvalTouchedResourceState
{
    /// <summary> Indicates that the evaluated code did not provide a complete declaration. </summary>
    [UcliContractLiteral("unknown")]
    Unknown = 1,

    /// <summary> Indicates that the evaluated code explicitly declared no touched resources. </summary>
    [UcliContractLiteral("none")]
    None = 2,

    /// <summary> Indicates that the evaluated code declared one or more touched resources. </summary>
    [UcliContractLiteral("declared")]
    Declared = 3,
}
