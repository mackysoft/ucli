namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Represents the selected read data source. </summary>
[VocabularyDefinition]
internal enum ReadIndexInfoSource
{
    /// <summary> A persisted read-index artifact supplied the selected result. </summary>
    [VocabularyText("index")]
    Index = 0,

    /// <summary> Live Unity execution supplied the selected result. </summary>
    [VocabularyText("unity")]
    Unity = 1,
}
