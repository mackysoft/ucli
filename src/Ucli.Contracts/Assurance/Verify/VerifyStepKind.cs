using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Identifies a verifier step in a verify profile. </summary>
public enum VerifyStepKind
{
    /// <summary> Evaluates Unity readiness. </summary>
    [UcliContractLiteral("ready")]
    Ready = 1,

    /// <summary> Runs compile assurance. </summary>
    [UcliContractLiteral("compile")]
    Compile = 2,

    /// <summary> Evaluates post-read operation evidence. </summary>
    [UcliContractLiteral("postRead")]
    PostRead = 3,

    /// <summary> Runs Unity tests. </summary>
    [UcliContractLiteral("test")]
    Test = 4,

    /// <summary> Collects Unity logs after non-passing claims. </summary>
    [UcliContractLiteral("logs")]
    Logs = 5,
}
