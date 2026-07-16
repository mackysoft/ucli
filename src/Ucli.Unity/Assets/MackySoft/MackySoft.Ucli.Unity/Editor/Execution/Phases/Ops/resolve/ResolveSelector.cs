using System;
using MackySoft.Ucli.Contracts.Ipc;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one validated <c>ucli.resolve</c> selector. </summary>
    internal sealed class ResolveSelector
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ResolveSelector" /> class.
        /// </summary>
        /// <param name="kind"> The resolved selector kind. </param>
        /// <param name="globalObjectId"> The GlobalObjectId selector literal. </param>
        /// <param name="assetGuid"> The asset GUID selector value. </param>
        /// <param name="assetPath"> The asset path selector literal. </param>
        /// <param name="projectAssetPath"> The project-scoped asset path selector literal. </param>
        /// <param name="scenePath"> The scene path selector literal. </param>
        /// <param name="prefabPath"> The prefab path selector literal. </param>
        /// <param name="hierarchyPath"> The hierarchy path selector literal. </param>
        /// <param name="componentType"> The optional component type selector literal. </param>
        private ResolveSelector (
            ResolveSelectorKind kind,
            UnityGlobalObjectId? globalObjectId,
            Guid? assetGuid,
            UnityAssetPath? assetPath,
            ProjectSettingsAssetPath? projectAssetPath,
            SceneAssetPath? scenePath,
            PrefabAssetPath? prefabPath,
            UnityHierarchyPath? hierarchyPath,
            UnityComponentTypeId? componentType)
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
        /// Gets the asset GUID. <see langword="null" /> for selectors that do not target an asset GUID.
        /// </summary>
        public Guid? AssetGuid { get; }

        /// <summary>
        /// Gets the asset path literal. <see langword="null" /> for selectors that do not target an asset path.
        /// </summary>
        public UnityAssetPath? AssetPath { get; }

        /// <summary>
        /// Gets the project-scoped asset path literal. <see langword="null" /> for selectors that do not target a project asset path.
        /// </summary>
        public ProjectSettingsAssetPath? ProjectAssetPath { get; }

        /// <summary>
        /// Gets the scene asset path. <see langword="null" /> for non-scene selectors.
        /// </summary>
        public SceneAssetPath? ScenePath { get; }

        /// <summary>
        /// Gets the prefab asset path. <see langword="null" /> for non-prefab selectors.
        /// </summary>
        public PrefabAssetPath? PrefabPath { get; }

        /// <summary>
        /// Gets the hierarchy path inside the scene or prefab. <see langword="null" /> for non-hierarchy selectors.
        /// </summary>
        public UnityHierarchyPath? HierarchyPath { get; }

        /// <summary>
        /// Gets the optional component type selector. <see langword="null" /> for GameObject or non-hierarchy selectors.
        /// </summary>
        public UnityComponentTypeId? ComponentType { get; }

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
        /// <param name="assetGuid"> The non-empty asset GUID. </param>
        /// <returns> One selector that resolves by asset GUID. </returns>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="assetGuid" /> is <see cref="Guid.Empty" />. </exception>
        public static ResolveSelector FromAssetGuid (Guid assetGuid)
        {
            if (assetGuid == Guid.Empty)
            {
                throw new ArgumentException("Asset GUID must not be empty.", nameof(assetGuid));
            }

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
        public static ResolveSelector FromAssetPath (UnityAssetPath assetPath)
        {
            if (assetPath == null)
            {
                throw new ArgumentNullException(nameof(assetPath));
            }

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
        public static ResolveSelector FromProjectAssetPath (ProjectSettingsAssetPath projectAssetPath)
        {
            if (projectAssetPath == null)
            {
                throw new ArgumentNullException(nameof(projectAssetPath));
            }

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
            SceneAssetPath scenePath,
            UnityHierarchyPath hierarchyPath,
            UnityComponentTypeId? componentType)
        {
            if (scenePath == null)
            {
                throw new ArgumentNullException(nameof(scenePath));
            }

            if (hierarchyPath == null)
            {
                throw new ArgumentNullException(nameof(hierarchyPath));
            }

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
            PrefabAssetPath prefabPath,
            UnityHierarchyPath hierarchyPath,
            UnityComponentTypeId? componentType)
        {
            if (prefabPath == null)
            {
                throw new ArgumentNullException(nameof(prefabPath));
            }

            if (hierarchyPath == null)
            {
                throw new ArgumentNullException(nameof(hierarchyPath));
            }

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
