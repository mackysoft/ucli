
namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Defines read-index freshness classifications returned by index evaluation. </summary>
[VocabularyDefinition]
public enum IndexFreshness
{
    /// <summary> Indicates that all tracked inputs match the index inputs snapshot. </summary>
    [VocabularyText("fresh")]
    Fresh = 1,

    /// <summary> Indicates that freshness could not be proven due to missing inputs or unavailable snapshots. </summary>
    [VocabularyText("probable")]
    Probable = 2,

    /// <summary> Indicates that tracked inputs differ from index inputs snapshot. </summary>
    [VocabularyText("stale")]
    Stale = 3,
}
