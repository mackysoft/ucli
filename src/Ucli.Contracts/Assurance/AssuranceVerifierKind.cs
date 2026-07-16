using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Identifies the verifier behavior represented by an assurance verifier entry. </summary>
public enum AssuranceVerifierKind
{
    /// <summary> Evaluates Unity readiness. </summary>
    [UcliContractLiteral("ready")]
    Ready = 1,

    /// <summary> Evaluates Unity script compilation. </summary>
    [UcliContractLiteral("compile")]
    Compile = 2,

    /// <summary> Evaluates a Unity player build. </summary>
    [UcliContractLiteral("build")]
    Build = 3,

    /// <summary> Evaluates the read surface after mutation. </summary>
    [UcliContractLiteral("postRead")]
    PostRead = 4,

    /// <summary> Evaluates Unity tests. </summary>
    [UcliContractLiteral("test")]
    Test = 5,

    /// <summary> Captures Unity logs as assurance evidence. </summary>
    [UcliContractLiteral("logs")]
    Logs = 6,
}
