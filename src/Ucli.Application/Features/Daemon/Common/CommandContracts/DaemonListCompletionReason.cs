using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;

/// <summary> Identifies why daemon-list output is partial. </summary>
internal enum DaemonListCompletionReason
{
    [UcliContractLiteral("timeout")]
    Timeout,
}
