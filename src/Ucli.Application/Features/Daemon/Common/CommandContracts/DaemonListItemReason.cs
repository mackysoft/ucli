using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;

/// <summary> Identifies why one daemon-list item is not reported as running. </summary>
internal enum DaemonListItemReason
{
    [UcliContractLiteral("staleSession")]
    StaleSession,

    [UcliContractLiteral("invalidSession")]
    InvalidSession,

    [UcliContractLiteral("probeTimeout")]
    ProbeTimeout,

    [UcliContractLiteral("probeFailed")]
    ProbeFailed,
}
