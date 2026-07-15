using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Identifies completeness of one scene-tree-lite node's child collection. </summary>
public enum IndexSceneTreeLiteNodeChildrenState
{
    /// <summary> All direct children are included. </summary>
    [UcliContractLiteral("complete")]
    Complete = 1,

    /// <summary> Direct children were not expanded because the requested depth was reached. </summary>
    [UcliContractLiteral("notExpandedByDepth")]
    NotExpandedByDepth = 2,

    /// <summary> Direct children were truncated by the bounded result window. </summary>
    [UcliContractLiteral("truncatedByWindow")]
    TruncatedByWindow = 3,

    /// <summary> Completeness cannot be determined from the source. </summary>
    [UcliContractLiteral("unknown")]
    Unknown = 4,
}
