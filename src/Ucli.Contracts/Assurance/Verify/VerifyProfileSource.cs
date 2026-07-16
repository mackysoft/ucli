using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Identifies how an effective verify profile was supplied. </summary>
public enum VerifyProfileSource
{
    /// <summary> The profile is built into uCLI. </summary>
    [UcliContractLiteral("builtIn")]
    BuiltIn = 1,

    /// <summary> The profile was loaded from a repository file. </summary>
    [UcliContractLiteral("file")]
    File = 2,
}
