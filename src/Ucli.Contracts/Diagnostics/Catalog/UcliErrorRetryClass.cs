using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts;

/// <summary> Classifies the default retry safety of a uCLI error code. </summary>
public enum UcliErrorRetryClass
{
    /// <summary> Indicates that replay is safe without an additional state check. </summary>
    [UcliContractLiteral("yes")]
    Yes = 1,

    /// <summary> Indicates that replay is not safe. </summary>
    [UcliContractLiteral("no")]
    No = 2,

    /// <summary> Indicates that callers should wait for the blocking condition to clear before retrying. </summary>
    [UcliContractLiteral("waitThenRetry")]
    WaitThenRetry = 3,

    /// <summary> Indicates that callers must plan again before retrying the call. </summary>
    [UcliContractLiteral("replanRequired")]
    ReplanRequired = 4,

    /// <summary> Indicates that retry safety depends on response evidence or diagnostics. </summary>
    [UcliContractLiteral("contextDependent")]
    ContextDependent = 5,

    /// <summary> Indicates that retry safety is unknown to this client. </summary>
    [UcliContractLiteral("unknown")]
    Unknown = 6,
}
