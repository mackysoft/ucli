using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;

/// <summary> Defines daemon status query outcomes. </summary>
internal enum DaemonStatusKind
{
    [UcliContractLiteral("running")]
    Running = 0,

    [UcliContractLiteral("notRunning")]
    NotRunning = 1,

    [UcliContractLiteral("stale")]
    Stale = 2,

    [UcliContractLiteralIgnore]
    Failed = 3,
}
