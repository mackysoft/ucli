
namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Identifies the resolved read-index mode represented by ready assurance evidence. </summary>
[VocabularyDefinition]
internal enum ReadyReadIndexMode
{
    /// <summary> Mode resolution failed before a configured mode was available. </summary>
    [VocabularyText("unknown")]
    Unknown = 1,

    /// <summary> Read-index access is disabled. </summary>
    [VocabularyText("disabled")]
    Disabled = 2,

    /// <summary> Stale read-index artifacts are allowed. </summary>
    [VocabularyText("allowStale")]
    AllowStale = 3,

    /// <summary> Fresh read-index artifacts are required. </summary>
    [VocabularyText("requireFresh")]
    RequireFresh = 4,
}
