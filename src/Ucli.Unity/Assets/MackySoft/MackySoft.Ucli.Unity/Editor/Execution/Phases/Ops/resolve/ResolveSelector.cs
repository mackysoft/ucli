using System;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one parsed <c>ucli.resolve</c> selector. </summary>
    internal readonly struct ResolveSelector
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ResolveSelector" /> struct.
        /// </summary>
        /// <param name="kind"> The resolved selector kind. </param>
        /// <param name="globalObjectId"> The GlobalObjectId selector literal. </param>
        /// <param name="assetGuid"> The asset GUID selector literal. </param>
        /// <param name="assetPath"> The asset path selector literal. </param>
        /// <param name="projectAssetPath"> The project-scoped asset path selector literal. </param>
        /// <param name="scenePath"> The scene path selector literal. </param>
        /// <param name="prefabPath"> The prefab path selector literal. </param>
        /// <param name="hierarchyPath"> The hierarchy path selector literal. </param>
        /// <param name="componentType"> The optional component type selector literal. </param>
        public ResolveSelector (
            ResolveSelectorKind kind,
            UnityGlobalObjectId? globalObjectId,
            string? assetGuid,
            string? assetPath,
            string? projectAssetPath,
            string? scenePath,
            string? prefabPath,
            string? hierarchyPath,
            string? componentType)
        {
            Kind = kind;
            GlobalObjectId = globalObjectId;
            AssetGuid = assetGuid;
            AssetPath = assetPath;
            ProjectAssetPath = projectAssetPath;
            ScenePath = scenePath;
            PrefabPath = prefabPath;
            HierarchyPath = hierarchyPath;
            ComponentType = componentType;
        }

        /// <summary>
        /// Gets the normalized selector kind.
        /// </summary>
        public ResolveSelectorKind Kind { get; }

        /// <summary>
        /// Gets the parsed GlobalObjectId. <see langword="null" /> for non-GlobalObjectId selectors.
        /// </summary>
        public UnityGlobalObjectId? GlobalObjectId { get; }

        /// <summary>
        /// Gets the asset GUID literal. <see langword="null" /> for selectors that do not target an asset GUID.
        /// </summary>
        public string? AssetGuid { get; }

        /// <summary>
        /// Gets the asset path literal. <see langword="null" /> for selectors that do not target an asset path.
        /// </summary>
        public string? AssetPath { get; }

        /// <summary>
        /// Gets the project-scoped asset path literal. <see langword="null" /> for selectors that do not target a project asset path.
        /// </summary>
        public string? ProjectAssetPath { get; }

        /// <summary>
        /// Gets the scene asset path. <see langword="null" /> for non-scene selectors.
        /// </summary>
        public string? ScenePath { get; }

        /// <summary>
        /// Gets the prefab asset path. <see langword="null" /> for non-prefab selectors.
        /// </summary>
        public string? PrefabPath { get; }

        /// <summary>
        /// Gets the hierarchy path inside the scene or prefab. <see langword="null" /> for non-hierarchy selectors.
        /// </summary>
        public string? HierarchyPath { get; }

        /// <summary>
        /// Gets the optional component type selector. <see langword="null" /> for GameObject or non-hierarchy selectors.
        /// </summary>
        public string? ComponentType { get; }

        /// <summary>
        /// Creates one GlobalObjectId selector.
        /// </summary>
        /// <param name="globalObjectId"> The parsed GlobalObjectId. </param>
        /// <returns> One selector that resolves by GlobalObjectId. </returns>
        public static ResolveSelector FromGlobalObjectId (UnityGlobalObjectId globalObjectId)
        {
            if (globalObjectId == null)
            {
                throw new ArgumentNullException(nameof(globalObjectId));
            }

            return new ResolveSelector(
                kind: ResolveSelectorKind.GlobalObjectId,
                globalObjectId: globalObjectId,
                assetGuid: null,
                assetPath: null,
                projectAssetPath: null,
                scenePath: null,
                prefabPath: null,
                hierarchyPath: null,
                componentType: null);
        }

        /// <summary>
        /// Creates one asset-GUID selector.
        /// </summary>
        /// <param name="assetGuid"> The asset GUID literal. </param>
        /// <returns> One selector that resolves by asset GUID. </returns>
        public static ResolveSelector FromAssetGuid (string assetGuid)
        {
            return new ResolveSelector(
                kind: ResolveSelectorKind.AssetGuid,
                globalObjectId: null,
                assetGuid: assetGuid,
                assetPath: null,
                projectAssetPath: null,
                scenePath: null,
                prefabPath: null,
                hierarchyPath: null,
                componentType: null);
        }

        /// <summary>
        /// Creates one asset-path selector.
        /// </summary>
        /// <param name="assetPath"> The asset path literal. </param>
        /// <returns> One selector that resolves by asset path. </returns>
        public static ResolveSelector FromAssetPath (string assetPath)
        {
            return new ResolveSelector(
                kind: ResolveSelectorKind.AssetPath,
                globalObjectId: null,
                assetGuid: null,
                assetPath: assetPath,
                projectAssetPath: null,
                scenePath: null,
                prefabPath: null,
                hierarchyPath: null,
                componentType: null);
        }

        /// <summary>
        /// Creates one project-asset-path selector.
        /// </summary>
        /// <param name="projectAssetPath"> The project-scoped asset path literal. </param>
        /// <returns> One selector that resolves by project-scoped asset path. </returns>
        public static ResolveSelector FromProjectAssetPath (string projectAssetPath)
        {
            return new ResolveSelector(
                kind: ResolveSelectorKind.ProjectAssetPath,
                globalObjectId: null,
                assetGuid: null,
                assetPath: null,
                projectAssetPath: projectAssetPath,
                scenePath: null,
                prefabPath: null,
                hierarchyPath: null,
                componentType: null);
        }

        /// <summary>
        /// Creates one scene hierarchy selector.
        /// </summary>
        /// <param name="scenePath"> The owning scene asset path. </param>
        /// <param name="hierarchyPath"> The hierarchy path inside the scene. </param>
        /// <param name="componentType"> The optional component type selector. <see langword="null" /> selects the GameObject. </param>
        /// <returns> One selector that resolves inside the specified scene. </returns>
        public static ResolveSelector FromSceneHierarchy (
            string scenePath,
            string hierarchyPath,
            string? componentType)
        {
            return new ResolveSelector(
                kind: componentType == null
                    ? ResolveSelectorKind.SceneHierarchyPath
                    : ResolveSelectorKind.SceneComponent,
                globalObjectId: null,
                assetGuid: null,
                assetPath: null,
                projectAssetPath: null,
                scenePath: scenePath,
                prefabPath: null,
                hierarchyPath: hierarchyPath,
                componentType: componentType);
        }

        /// <summary>
        /// Creates one prefab hierarchy selector.
        /// </summary>
        /// <param name="prefabPath"> The owning prefab asset path. </param>
        /// <param name="hierarchyPath"> The hierarchy path inside the prefab root. </param>
        /// <param name="componentType"> The optional component type selector. <see langword="null" /> selects the GameObject. </param>
        /// <returns> One selector that resolves inside the specified prefab. </returns>
        public static ResolveSelector FromPrefabHierarchy (
            string prefabPath,
            string hierarchyPath,
            string? componentType)
        {
            return new ResolveSelector(
                kind: componentType == null
                    ? ResolveSelectorKind.PrefabHierarchyPath
                    : ResolveSelectorKind.PrefabComponent,
                globalObjectId: null,
                assetGuid: null,
                assetPath: null,
                projectAssetPath: null,
                scenePath: null,
                prefabPath: prefabPath,
                hierarchyPath: hierarchyPath,
                componentType: componentType);
        }
    }
}
