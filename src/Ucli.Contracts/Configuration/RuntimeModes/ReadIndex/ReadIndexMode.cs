using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Configuration;

/// <summary> Defines read-index usage modes shared by read commands. </summary>
public enum ReadIndexMode
{
    /// <summary> Disables read-index usage and bypasses index generation. </summary>
    [UcliContractLiteral("disabled")]
    Disabled = 0,

    /// <summary> Uses read-index and allows stale or probable freshness states. </summary>
    [UcliContractLiteral("allowStale")]
    AllowStale = 1,

    /// <summary> Uses read-index and requires <c>fresh</c> freshness state. </summary>
    [UcliContractLiteral("requireFresh")]
    RequireFresh = 2,
}
