using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Daemon;

/// <summary> Defines daemon startup retry-disposition literals. </summary>
public enum DaemonStartupRetryDisposition
{
    /// <summary> Caller may retry immediately. </summary>
    [UcliContractLiteral("retryImmediately")]
    RetryImmediately = 0,

    /// <summary> Caller should wait before retrying. </summary>
    [UcliContractLiteral("waitThenRetry")]
    WaitThenRetry = 1,

    /// <summary> Caller may retry after fixing the reported blocker. </summary>
    [UcliContractLiteral("retryAfterFix")]
    RetryAfterFix = 2,

    /// <summary> Caller must perform manual action before retrying. </summary>
    [UcliContractLiteral("manualActionRequired")]
    ManualActionRequired = 3,

    /// <summary> Caller should not retry this startup attempt. </summary>
    [UcliContractLiteral("doNotRetry")]
    DoNotRetry = 4,

    /// <summary> Retry disposition is unknown. </summary>
    [UcliContractLiteral("unknown")]
    Unknown = 5,
}
