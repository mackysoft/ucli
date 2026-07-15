using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;

/// <summary> Defines daemon cleanup outcome states. </summary>
internal enum DaemonCleanupStatus
{
    /// <summary> Indicates cleanup completed successfully. </summary>
    [UcliContractLiteral("completed")]
    Completed = 0,

    /// <summary> Indicates cleanup was intentionally skipped for safety. </summary>
    [UcliContractLiteral("skipped")]
    Skipped = 1,
}
