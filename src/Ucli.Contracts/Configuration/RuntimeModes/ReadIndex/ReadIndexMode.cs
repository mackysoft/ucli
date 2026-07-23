
namespace MackySoft.Ucli.Contracts.Configuration;

/// <summary> Defines read-index usage modes shared by read commands. </summary>
[VocabularyDefinition]
public enum ReadIndexMode
{
    /// <summary> Disables read-index usage and bypasses index generation. </summary>
    [VocabularyText("disabled")]
    Disabled = 0,

    /// <summary> Uses read-index and allows stale or probable freshness states. </summary>
    [VocabularyText("allowStale")]
    AllowStale = 1,

    /// <summary> Uses read-index and requires <c>fresh</c> freshness state. </summary>
    [VocabularyText("requireFresh")]
    RequireFresh = 2,
}
