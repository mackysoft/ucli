namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents selector kinds supported by <c>ucli.resolve</c>. </summary>
    internal enum ResolveSelectorKind
    {
        GlobalObjectId = 1,
        AssetGuid = 2,
        AssetPath = 3,
        SceneHierarchyPath = 4,
        PrefabHierarchyPath = 5,
        SceneComponent = 6,
        PrefabComponent = 7,
        ProjectAssetPath = 8,
    }
}
