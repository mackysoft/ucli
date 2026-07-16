using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Identifies the resolved read-index mode represented by ready assurance evidence. </summary>
internal enum ReadyReadIndexMode
{
    /// <summary> Mode resolution failed before a configured mode was available. </summary>
    [UcliContractLiteral("unknown")]
    Unknown = 1,

    /// <summary> Read-index access is disabled. </summary>
    [UcliContractLiteral("disabled")]
    Disabled = 2,

    /// <summary> Stale read-index artifacts are allowed. </summary>
    [UcliContractLiteral("allowStale")]
    AllowStale = 3,

    /// <summary> Fresh read-index artifacts are required. </summary>
    [UcliContractLiteral("requireFresh")]
    RequireFresh = 4,
}
