using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;

/// <summary> Identifies one daemon-list item observation state. </summary>
internal enum DaemonListItemState
{
    [UcliContractLiteral("running")]
    Running,

    [UcliContractLiteral("stale")]
    Stale,

    [UcliContractLiteral("error")]
    Error,
}
