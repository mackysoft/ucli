using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Defines read-index freshness classifications returned by index evaluation. </summary>
public enum IndexFreshness
{
    /// <summary> Indicates that all tracked inputs match the index inputs snapshot. </summary>
    [UcliContractLiteral("fresh")]
    Fresh = 1,

    /// <summary> Indicates that freshness could not be proven due to missing inputs or unavailable snapshots. </summary>
    [UcliContractLiteral("probable")]
    Probable = 2,

    /// <summary> Indicates that tracked inputs differ from index inputs snapshot. </summary>
    [UcliContractLiteral("stale")]
    Stale = 3,
}
