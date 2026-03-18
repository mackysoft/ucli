namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents selector kinds supported by <c>ucli.resolve</c>. </summary>
    internal enum ResolveSelectorKind
    {
        GlobalObjectId = 1,
        AssetGuid = 2,
        AssetPath = 3,
        SceneHierarchyPath = 4,
    }
}