
namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Identifies completeness of one scene-tree-lite node's child collection. </summary>
[VocabularyDefinition]
public enum IndexSceneTreeLiteNodeChildrenState
{
    /// <summary> All direct children are included. </summary>
    [VocabularyText("complete")]
    Complete = 1,

    /// <summary> Direct children were not expanded because the requested depth was reached. </summary>
    [VocabularyText("notExpandedByDepth")]
    NotExpandedByDepth = 2,

    /// <summary> Direct children were truncated by the bounded result window. </summary>
    [VocabularyText("truncatedByWindow")]
    TruncatedByWindow = 3,

    /// <summary> Completeness cannot be determined from the source. </summary>
    [VocabularyText("unknown")]
    Unknown = 4,
}
