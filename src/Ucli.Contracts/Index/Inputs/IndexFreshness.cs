namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Defines read-index freshness classifications returned by index evaluation. </summary>
public enum IndexFreshness
{
    /// <summary> Indicates that all tracked inputs match the index inputs snapshot. </summary>
    Fresh = 0,

    /// <summary> Indicates that freshness could not be proven due to missing inputs or unavailable snapshots. </summary>
    Probable = 1,

    /// <summary> Indicates that tracked inputs differ from index inputs snapshot. </summary>
    Stale = 2,
}
