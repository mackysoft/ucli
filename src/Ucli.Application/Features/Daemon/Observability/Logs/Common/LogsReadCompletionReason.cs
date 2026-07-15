using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;

/// <summary> Identifies how a log-read operation completed. </summary>
internal enum LogsReadCompletionReason
{
    /// <summary> The bounded read or stream completed normally. </summary>
    [UcliContractLiteral("completed")]
    Completed = 1,

    /// <summary> The stream stopped after its idle timeout elapsed. </summary>
    [UcliContractLiteral("idleTimeout")]
    IdleTimeout = 2,

    /// <summary> The stream reached its inclusive upper timestamp bound. </summary>
    [UcliContractLiteral("untilReached")]
    UntilReached = 3,

    /// <summary> The caller canceled the read. </summary>
    [UcliContractLiteral("canceled")]
    Canceled = 4,

    /// <summary> The read failed. </summary>
    [UcliContractLiteral("error")]
    Error = 5,
}
