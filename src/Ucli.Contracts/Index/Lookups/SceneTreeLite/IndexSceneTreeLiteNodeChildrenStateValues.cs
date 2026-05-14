namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Defines scene-tree-lite node child completeness state values. </summary>
public static class IndexSceneTreeLiteNodeChildrenStateValues
{
    /// <summary> All direct children are included. </summary>
    public const string Complete = "complete";

    /// <summary> Direct children were not expanded because the requested depth was reached. </summary>
    public const string NotExpandedByDepth = "notExpandedByDepth";

    /// <summary> Direct children were truncated by the bounded result window. </summary>
    public const string TruncatedByWindow = "truncatedByWindow";

    /// <summary> Completeness cannot be determined from the source. </summary>
    public const string Unknown = "unknown";
}
