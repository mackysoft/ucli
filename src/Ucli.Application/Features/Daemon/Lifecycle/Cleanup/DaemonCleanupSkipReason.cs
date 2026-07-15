using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;

/// <summary> Defines daemon cleanup skip reasons. </summary>
internal enum DaemonCleanupSkipReason
{
    /// <summary> Indicates cleanup was skipped because daemon is running. </summary>
    [UcliContractLiteral("running")]
    Running = 1,

    /// <summary> Indicates cleanup was skipped because invalid session may still belong to a live daemon. </summary>
    [UcliContractLiteral("unsafeInvalidSession")]
    UnsafeInvalidSession = 2,

    /// <summary> Indicates cleanup was skipped because reachability could not be determined safely. </summary>
    [UcliContractLiteral("uncertainReachability")]
    UncertainReachability = 3,
}
