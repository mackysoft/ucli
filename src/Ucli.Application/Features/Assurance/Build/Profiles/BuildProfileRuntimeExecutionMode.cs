using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;

/// <summary> Defines build profile runtime execution-mode literals. </summary>
internal enum BuildProfileRuntimeExecutionMode
{
    /// <summary> Allows daemon execution. </summary>
    [UcliContractLiteral("daemon")]
    Daemon = 0,

    /// <summary> Allows oneshot execution. </summary>
    [UcliContractLiteral("oneshot")]
    Oneshot = 1,
}
