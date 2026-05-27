using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;

/// <summary> Defines daemon stop operation outcomes. </summary>
internal enum DaemonStopStatus
{
    [UcliContractLiteral("stopped")]
    Stopped = 0,

    [UcliContractLiteral("notRunning")]
    NotRunning = 1,

    [UcliContractLiteralIgnore]
    Failed = 2,
}
