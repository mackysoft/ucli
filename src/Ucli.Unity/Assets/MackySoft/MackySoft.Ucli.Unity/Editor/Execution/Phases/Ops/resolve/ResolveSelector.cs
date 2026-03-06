namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one parsed <c>ucli.resolve</c> selector. </summary>
    internal readonly struct ResolveSelector
    {
        public ResolveSelector (
            ResolveSelectorKind kind,
            string? globalObjectId,
            string? assetGuid,
            string? assetPath,
            string? scenePath,
            string? hierarchyPath)
        {
            Kind = kind;
            GlobalObjectId = globalObjectId;
            AssetGuid = assetGuid;
            AssetPath = assetPath;
            ScenePath = scenePath;
            HierarchyPath = hierarchyPath;
        }

        public ResolveSelectorKind Kind { get; }

        public string? GlobalObjectId { get; }

        public string? AssetGuid { get; }

        public string? AssetPath { get; }

        public string? ScenePath { get; }

        public string? HierarchyPath { get; }

        public static ResolveSelector FromGlobalObjectId (string globalObjectId)
        {
            return new ResolveSelector(
                kind: ResolveSelectorKind.GlobalObjectId,
                globalObjectId: globalObjectId,
                assetGuid: null,
                assetPath: null,
                scenePath: null,
                hierarchyPath: null);
        }

        public static ResolveSelector FromAssetGuid (string assetGuid)
        {
            return new ResolveSelector(
                kind: ResolveSelectorKind.AssetGuid,
                globalObjectId: null,
                assetGuid: assetGuid,
                assetPath: null,
                scenePath: null,
                hierarchyPath: null);
        }

        public static ResolveSelector FromAssetPath (string assetPath)
        {
            return new ResolveSelector(
                kind: ResolveSelectorKind.AssetPath,
                globalObjectId: null,
                assetGuid: null,
                assetPath: assetPath,
                scenePath: null,
                hierarchyPath: null);
        }

        public static ResolveSelector FromSceneHierarchy (
            string scenePath,
            string hierarchyPath)
        {
            return new ResolveSelector(
                kind: ResolveSelectorKind.SceneHierarchyPath,
                globalObjectId: null,
                assetGuid: null,
                assetPath: null,
                scenePath: scenePath,
                hierarchyPath: hierarchyPath);
        }
    }
}
