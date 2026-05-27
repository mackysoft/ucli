using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Contracts;

/// <summary> Defines daemon start operation outcomes. </summary>
internal enum DaemonStartStatus
{
    [UcliContractLiteral("started")]
    Started = 0,

    [UcliContractLiteral("alreadyRunning")]
    AlreadyRunning = 1,

    [UcliContractLiteral("failed")]
    Failed = 2,

    [UcliContractLiteral("attached")]
    Attached = 3,
}
