using System;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one per-request execution context shared by operation phases. </summary>
    public sealed class OperationExecutionContext : IDisposable
    {
        private readonly TemporaryAliasRegistry temporaryAliasRegistry = new TemporaryAliasRegistry();

        private readonly ComponentSandboxRegistry componentSandboxRegistry = new ComponentSandboxRegistry();

        private readonly AssetSandboxRegistry assetSandboxRegistry = new AssetSandboxRegistry();

        private readonly PlannedAssetRegistry plannedAssetRegistry = new PlannedAssetRegistry();

        private readonly TemporarySceneRegistry temporarySceneRegistry = new TemporarySceneRegistry();

        private readonly TemporaryObjectScope temporaryObjectScope = new TemporaryObjectScope();

        private readonly RequestAttributedChangeRegistry requestAttributedChangeRegistry = new RequestAttributedChangeRegistry();

        private readonly HashSet<string> plannedLiveSceneOpenPaths = new HashSet<string>(StringComparer.Ordinal);

        private readonly HashSet<string> plannedLivePrefabOpenPaths = new HashSet<string>(StringComparer.Ordinal);

        private bool disposed;

        /// <summary> Initializes a new instance of the <see cref="OperationExecutionContext" /> class. </summary>
        public OperationExecutionContext ()
            : this(new OperationAliasStore())
        {
        }

        /// <summary> Initializes a new instance of the <see cref="OperationExecutionContext" /> class. </summary>
        /// <param name="aliasStore"> The alias-store dependency. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="aliasStore" /> is <see langword="null" />. </exception>
        internal OperationExecutionContext (OperationAliasStore aliasStore)
        {
            AliasStore = aliasStore ?? throw new ArgumentNullException(nameof(aliasStore));
        }

        /// <summary> Gets the alias store used to share resolved references within one request. </summary>
        internal OperationAliasStore AliasStore { get; }

        /// <summary> Stores or replaces one temporary alias value used during plan execution. </summary>
        /// <param name="alias"> The alias name. </param>
        /// <param name="unityObject"> The temporary live object. </param>
        /// <param name="resource"> The logical owner resource associated with the temporary object. </param>
        /// <param name="sourceGlobalObjectId"> The optional source GlobalObjectId used to synchronize shadows. </param>
        internal void SetTemporaryAlias (
            string alias,
            UnityEngine.Object unityObject,
            OperationResource resource,
            string? sourceGlobalObjectId = null)
        {
            temporaryAliasRegistry.Set(alias, unityObject, resource, sourceGlobalObjectId);
        }

        /// <summary> Tries to get one temporary alias state. </summary>
        /// <param name="alias"> The alias name. </param>
        /// <param name="state"> The tracked alias state when found. </param>
        /// <returns> <see langword="true" /> when temporary alias exists; otherwise <see langword="false" />. </returns>
        internal bool TryGetTemporaryAliasState (
            string alias,
            out TemporaryAliasRegistry.TemporaryAliasState state)
        {
            return temporaryAliasRegistry.TryGetState(alias, out state);
        }

        /// <summary> Stores or replaces one plan-time ensured component keyed by target GameObject and component type. </summary>
        /// <param name="targetGlobalObjectId"> The source GameObject GlobalObjectId. </param>
        /// <param name="componentType"> The ensured component runtime type. </param>
        /// <param name="component"> The temporary ensured component. </param>
        /// <param name="resource"> The owning resource. </param>
        internal void SetEnsuredComponent (
            string targetGlobalObjectId,
            Type componentType,
            Component component,
            OperationResource resource)
        {
            componentSandboxRegistry.SetEnsuredComponent(targetGlobalObjectId, componentType, component, resource);
        }

        /// <summary> Tries to get one plan-time ensured component state keyed by target GameObject and component type. </summary>
        /// <param name="targetGlobalObjectId"> The source GameObject GlobalObjectId. </param>
        /// <param name="componentType"> The ensured component runtime type. </param>
        /// <param name="state"> The ensured component state when found. </param>
        /// <returns> <see langword="true" /> when ensured component exists; otherwise <see langword="false" />. </returns>
        internal bool TryGetEnsuredComponentState (
            string targetGlobalObjectId,
            Type componentType,
            out ComponentSandboxRegistry.EnsuredComponentState state)
        {
            return componentSandboxRegistry.TryGetEnsuredComponentState(targetGlobalObjectId, componentType, out state);
        }

        /// <summary> Tries to resolve one tracked temporary component back to its logical owner resource. </summary>
        /// <param name="component"> The tracked temporary component. </param>
        /// <param name="resource"> The logical owner resource when found. </param>
        /// <returns> <see langword="true" /> when the component belongs to tracked plan-time state; otherwise <see langword="false" />. </returns>
        internal bool TryResolveTrackedComponentResource (
            Component component,
            out OperationResource resource)
        {
            return componentSandboxRegistry.TryResolveTrackedComponentResource(component, out resource);
        }

        /// <summary> Stores or replaces one temporary component shadow keyed by source GlobalObjectId. </summary>
        /// <param name="globalObjectId"> The source component GlobalObjectId. </param>
        /// <param name="component"> The temporary shadow component. </param>
        /// <param name="resource"> The owning resource. </param>
        internal void SetComponentShadow (
            string globalObjectId,
            Component component,
            OperationResource resource)
        {
            componentSandboxRegistry.SetComponentShadow(globalObjectId, component, resource, temporaryAliasRegistry);
        }

        /// <summary> Replaces tracked temporary component references that still point to an older plan-time component instance. </summary>
        /// <param name="sourceComponent"> The previous temporary component instance. </param>
        /// <param name="replacementComponent"> The replacement temporary component instance. </param>
        /// <param name="resource"> The owning resource. </param>
        internal void ReplaceTrackedTemporaryComponent (
            Component sourceComponent,
            Component replacementComponent,
            OperationResource resource)
        {
            componentSandboxRegistry.ReplaceTrackedTemporaryComponent(
                sourceComponent,
                replacementComponent,
                resource,
                temporaryAliasRegistry);
        }

        /// <summary> Tries to get one temporary component shadow state. </summary>
        /// <param name="globalObjectId"> The source component GlobalObjectId. </param>
        /// <param name="state"> The component shadow state when found. </param>
        /// <returns> <see langword="true" /> when shadow exists; otherwise <see langword="false" />. </returns>
        internal bool TryGetComponentShadowState (
            string globalObjectId,
            out ComponentSandboxRegistry.ComponentShadowState state)
        {
            return componentSandboxRegistry.TryGetComponentShadowState(globalObjectId, out state);
        }

        /// <summary> Tracks one temporary prefab-contents root for unload at the end of request execution. </summary>
        /// <param name="prefabPath"> The prefab asset path associated with the loaded contents. </param>
        /// <param name="prefabContentsRoot"> The loaded prefab-contents root. </param>
        internal void TrackTemporaryPrefabContentsRoot (
            string prefabPath,
            GameObject prefabContentsRoot)
        {
            temporaryObjectScope.TrackTemporaryPrefabContentsRoot(prefabPath, prefabContentsRoot);
        }

        /// <summary> Tries to get one request-local temporary prefab-contents root. </summary>
        /// <param name="prefabPath"> The prefab asset path. </param>
        /// <param name="prefabContentsRoot"> The loaded prefab-contents root when found. </param>
        /// <returns> <see langword="true" /> when tracked root exists; otherwise <see langword="false" />. </returns>
        internal bool TryGetTemporaryPrefabContentsRoot (
            string prefabPath,
            out GameObject? prefabContentsRoot)
        {
            return temporaryObjectScope.TryGetTemporaryPrefabContentsRoot(prefabPath, out prefabContentsRoot);
        }

        /// <summary> Tries to resolve one tracked temporary prefab asset path from a prefab-contents GameObject. </summary>
        /// <param name="gameObject"> The candidate GameObject. </param>
        /// <param name="prefabPath"> The tracked prefab asset path when found. </param>
        /// <returns> <see langword="true" /> when the GameObject belongs to tracked temporary prefab contents; otherwise <see langword="false" />. </returns>
        internal bool TryResolveTemporaryPrefabPath (
            GameObject gameObject,
            out string prefabPath)
        {
            return temporaryObjectScope.TryResolveTemporaryPrefabPath(gameObject, out prefabPath);
        }

        /// <summary> Tries to resolve one request-local preview scene object back to its mirrored live source object. </summary>
        /// <param name="scenePath"> The logical scene asset path. </param>
        /// <param name="previewObject"> The preview object. </param>
        /// <param name="sourceObject"> The mirrored live source object when found. </param>
        /// <returns> <see langword="true" /> when the preview object originated from a dirty loaded-scene mirror; otherwise <see langword="false" />. </returns>
        internal bool TryResolveTemporarySceneSourceObject (
            string scenePath,
            UnityEngine.Object previewObject,
            out UnityEngine.Object? sourceObject)
        {
            return temporarySceneRegistry.TryResolveMirroredSourceObject(scenePath, previewObject, out sourceObject);
        }

        /// <summary> Tries to resolve one request-local preview prefab object back to its mirrored live source object. </summary>
        /// <param name="prefabPath"> The logical prefab asset path. </param>
        /// <param name="previewObject"> The preview object. </param>
        /// <param name="sourceObject"> The mirrored live source object when found. </param>
        /// <returns> <see langword="true" /> when the preview object originated from an opened Prefab Stage mirror; otherwise <see langword="false" />. </returns>
        internal bool TryResolveTemporaryPrefabSourceObject (
            string prefabPath,
            UnityEngine.Object previewObject,
            out UnityEngine.Object? sourceObject)
        {
            return temporaryObjectScope.TryResolveMirroredSourceObject(prefabPath, previewObject, out sourceObject);
        }

        internal bool TryResolveTemporaryPrefabStableSourceObject (
            string prefabPath,
            UnityEngine.Object previewObject,
            out UnityEngine.Object? stableSourceObject)
        {
            return temporaryObjectScope.TryResolveMirroredStableSourceObject(prefabPath, previewObject, out stableSourceObject);
        }

        /// <summary> Resolves one scene path to the active execution session for the current request. </summary>
        /// <param name="scenePath"> The scene asset path. </param>
        /// <param name="createTemporaryIfMissing"> Whether one request-owned preview scene may be opened when the scene is not already loaded. </param>
        /// <param name="scene"> The resolved scene when successful. </param>
        /// <param name="isRequestOwned"> <see langword="true" /> when the resolved scene is one request-owned preview scene. </param>
        /// <param name="errorMessage"> The validation error message when resolution fails. </param>
        /// <returns> <see langword="true" /> when the scene can be resolved for request execution; otherwise <see langword="false" />. </returns>
        internal bool TryResolveSceneExecutionSession (
            string scenePath,
            bool createTemporaryIfMissing,
            out Scene scene,
            out bool isRequestOwned,
            out string errorMessage)
        {
            scene = default;
            isRequestOwned = false;
            if (SceneOperationUtilities.TryGetLoadedScene(scenePath, out scene, out _))
            {
                errorMessage = string.Empty;
                return true;
            }

            if (TryGetTemporaryScene(scenePath, out scene))
            {
                isRequestOwned = true;
                errorMessage = string.Empty;
                return true;
            }

            if (!createTemporaryIfMissing)
            {
                errorMessage = $"Scene is not loaded: {scenePath}. Use 'ucli.scene.open' first.";
                return false;
            }

            if (!TryGetOrOpenTemporaryScene(scenePath, out scene, out errorMessage))
            {
                return false;
            }

            isRequestOwned = true;
            errorMessage = string.Empty;
            return true;
        }

        /// <summary> Ensures that one scene path has request-local plan state available for implicit edit flow. </summary>
        /// <param name="scenePath"> The scene asset path. </param>
        /// <param name="errorMessage"> The validation error message when acquisition fails. </param>
        /// <returns> <see langword="true" /> when request-local scene state is available; otherwise <see langword="false" />. </returns>
        internal bool TryEnsureSceneExecutionSession (
            string scenePath,
            out string errorMessage)
        {
            if (TryGetTemporaryScene(scenePath, out _))
            {
                errorMessage = string.Empty;
                return true;
            }

            return TryGetOrOpenTemporaryScene(scenePath, out _, out errorMessage);
        }

        /// <summary> Resolves one prefab path to the active execution session for the current request. </summary>
        /// <param name="prefabPath"> The prefab asset path. </param>
        /// <param name="createTemporaryIfMissing"> Whether one request-owned prefab contents root may be loaded when the prefab stage is not already open. </param>
        /// <param name="prefabContentsRoot"> The resolved prefab contents root when successful. </param>
        /// <param name="prefabStage"> The opened prefab stage when the resolved session is live; otherwise <see langword="null" />. </param>
        /// <param name="isRequestOwned"> <see langword="true" /> when the resolved contents root is request-owned temporary state. </param>
        /// <param name="errorMessage"> The validation error message when resolution fails. </param>
        /// <returns> <see langword="true" /> when the prefab can be resolved for request execution; otherwise <see langword="false" />. </returns>
        internal bool TryResolvePrefabExecutionSession (
            string prefabPath,
            bool createTemporaryIfMissing,
            out GameObject? prefabContentsRoot,
            out PrefabStage? prefabStage,
            out bool isRequestOwned,
            out string errorMessage)
        {
            prefabContentsRoot = null;
            prefabStage = null;
            isRequestOwned = false;
            if (PrefabOperationUtilities.TryGetOpenedPrefabStage(prefabPath, out prefabStage, out _))
            {
                prefabContentsRoot = prefabStage!.prefabContentsRoot;
                if (prefabContentsRoot == null)
                {
                    errorMessage = $"Prefab root is not available after open: {prefabPath}.";
                    return false;
                }

                errorMessage = string.Empty;
                return true;
            }

            if (TryGetTemporaryPrefabContentsRoot(prefabPath, out prefabContentsRoot)
                && prefabContentsRoot != null)
            {
                isRequestOwned = true;
                errorMessage = string.Empty;
                return true;
            }

            if (!createTemporaryIfMissing)
            {
                errorMessage = $"Prefab is not opened: {prefabPath}. Use 'ucli.prefab.open' first.";
                return false;
            }

            if (!PrefabOperationUtilities.TryGetOrLoadTemporaryPrefabContentsRoot(
                    prefabPath,
                    this,
                    out prefabContentsRoot,
                    out errorMessage))
            {
                return false;
            }

            isRequestOwned = true;
            errorMessage = string.Empty;
            return true;
        }

        /// <summary> Ensures that one prefab path has request-local plan state available for implicit edit flow. </summary>
        /// <param name="prefabPath"> The prefab asset path. </param>
        /// <param name="errorMessage"> The validation error message when acquisition fails. </param>
        /// <returns> <see langword="true" /> when request-local prefab state is available; otherwise <see langword="false" />. </returns>
        internal bool TryEnsurePrefabExecutionSession (
            string prefabPath,
            out string errorMessage)
        {
            if (TryGetTemporaryPrefabContentsRoot(prefabPath, out var prefabContentsRoot)
                && prefabContentsRoot != null)
            {
                errorMessage = string.Empty;
                return true;
            }

            return TryGetOrCreateTemporaryPrefabContentsRoot(
                prefabPath,
                out _,
                out errorMessage);
        }

        /// <summary> Marks one persistence resource as changed by the current request. </summary>
        /// <param name="resource"> The changed resource. </param>
        internal void MarkRequestAttributedChange (OperationResource resource)
        {
            requestAttributedChangeRegistry.MarkChanged(resource);
        }

        /// <summary> Determines whether one persistence resource has been changed by the current request. </summary>
        /// <param name="resource"> The resource to test. </param>
        /// <returns> <see langword="true" /> when the request changed the resource; otherwise <see langword="false" />. </returns>
        internal bool HasRequestAttributedChange (OperationResource resource)
        {
            return requestAttributedChangeRegistry.Contains(resource);
        }

        /// <summary> Removes the request-attributed changed marker for one persistence resource. </summary>
        /// <param name="resource"> The resource whose changed marker should be cleared. </param>
        internal void UnmarkRequestAttributedChange (OperationResource resource)
        {
            requestAttributedChangeRegistry.UnmarkChanged(resource);
        }

        /// <summary> Tracks that one prior step planned an explicit live scene open for the specified path. </summary>
        /// <param name="scenePath"> The scene asset path. </param>
        internal void TrackPlannedLiveSceneOpen (string scenePath)
        {
            plannedLiveSceneOpenPaths.Add(scenePath);
        }

        /// <summary> Determines whether one prior step planned an explicit live scene open for the specified path. </summary>
        /// <param name="scenePath"> The scene asset path. </param>
        /// <returns> <see langword="true" /> when an explicit live scene open was planned earlier in this request; otherwise <see langword="false" />. </returns>
        internal bool HasPlannedLiveSceneOpen (string scenePath)
        {
            return plannedLiveSceneOpenPaths.Contains(scenePath);
        }

        /// <summary> Tracks that one prior step planned an explicit live prefab open for the specified path. </summary>
        /// <param name="prefabPath"> The prefab asset path. </param>
        internal void TrackPlannedLivePrefabOpen (string prefabPath)
        {
            plannedLivePrefabOpenPaths.Add(prefabPath);
        }

        /// <summary> Determines whether one prior step planned an explicit live prefab open for the specified path. </summary>
        /// <param name="prefabPath"> The prefab asset path. </param>
        /// <returns> <see langword="true" /> when an explicit live prefab open was planned earlier in this request; otherwise <see langword="false" />. </returns>
        internal bool HasPlannedLivePrefabOpen (string prefabPath)
        {
            return plannedLivePrefabOpenPaths.Contains(prefabPath);
        }

        /// <summary> Stores or replaces one temporary asset shadow keyed by source GlobalObjectId. </summary>
        /// <param name="globalObjectId"> The source asset GlobalObjectId. </param>
        /// <param name="unityObject"> The temporary asset shadow. </param>
        /// <param name="assetPath"> The asset path. </param>
        internal void SetAssetShadow (
            string globalObjectId,
            UnityEngine.Object unityObject,
            string assetPath)
        {
            assetSandboxRegistry.SetAssetShadow(globalObjectId, unityObject, assetPath, temporaryAliasRegistry);
        }

        /// <summary> Tries to get one temporary asset shadow. </summary>
        /// <param name="globalObjectId"> The source asset GlobalObjectId. </param>
        /// <param name="unityObject"> The temporary asset shadow when found. </param>
        /// <param name="assetPath"> The asset path when found. </param>
        /// <returns> <see langword="true" /> when shadow exists; otherwise <see langword="false" />. </returns>
        internal bool TryGetAssetShadow (
            string globalObjectId,
            out UnityEngine.Object? unityObject,
            out string assetPath)
        {
            return assetSandboxRegistry.TryGetAssetShadow(globalObjectId, out unityObject, out assetPath);
        }

        /// <summary> Stores or replaces one plan-time created asset keyed by its reserved asset path. </summary>
        /// <param name="assetPath"> The reserved asset path. </param>
        /// <param name="ownerExecutionKey"> The request-internal primitive execution key that owns the reservation. </param>
        /// <param name="unityObject"> The current temporary asset instance. </param>
        internal void SetPlannedAsset (
            string assetPath,
            string ownerExecutionKey,
            UnityEngine.Object unityObject)
        {
            plannedAssetRegistry.SetPlannedAsset(assetPath, ownerExecutionKey, unityObject, temporaryAliasRegistry);
        }

        /// <summary> Tries to get one plan-time created asset state keyed by asset path. </summary>
        /// <param name="assetPath"> The reserved asset path. </param>
        /// <param name="state"> The planned asset state when found. </param>
        /// <returns> <see langword="true" /> when the planned asset exists; otherwise <see langword="false" />. </returns>
        internal bool TryGetPlannedAssetState (
            string assetPath,
            out PlannedAssetRegistry.PlannedAssetState state)
        {
            return plannedAssetRegistry.TryGetState(assetPath, out state);
        }

        /// <summary> Tries to get one request-local preview scene. </summary>
        /// <param name="scenePath"> The scene asset path. </param>
        /// <param name="scene"> The preview scene when tracked. </param>
        /// <returns> <see langword="true" /> when the request already owns a preview scene for <paramref name="scenePath" />; otherwise <see langword="false" />. </returns>
        internal bool TryGetTemporaryScene (
            string scenePath,
            out Scene scene)
        {
            return temporarySceneRegistry.TryGetPreviewScene(scenePath, out scene);
        }

        /// <summary> Gets one request-local preview scene or creates one from the current loaded scene snapshot when needed. </summary>
        /// <param name="scenePath"> The scene asset path. </param>
        /// <param name="scene"> The preview scene when successful. </param>
        /// <param name="errorMessage"> The validation error message when preview scene acquisition fails. </param>
        /// <returns> <see langword="true" /> when a preview scene is available for <paramref name="scenePath" />; otherwise <see langword="false" />. </returns>
        internal bool TryGetOrOpenTemporaryScene (
            string scenePath,
            out Scene scene,
            out string errorMessage)
        {
            if (temporarySceneRegistry.TryGetPreviewScene(scenePath, out scene))
            {
                errorMessage = string.Empty;
                return true;
            }

            if (SceneOperationUtilities.TryGetLoadedScene(scenePath, out var loadedScene, out _)
                && loadedScene.isDirty)
            {
                return temporarySceneRegistry.TryGetOrCreatePreviewSceneFromLoadedScene(
                    scenePath,
                    loadedScene,
                    out scene,
                    out errorMessage);
            }

            return temporarySceneRegistry.TryGetOrOpenPreviewScene(scenePath, out scene, out errorMessage);
        }

        /// <summary> Gets one request-local temporary prefab root or mirrors the current opened prefab-stage snapshot when needed. </summary>
        /// <param name="prefabPath"> The prefab asset path. </param>
        /// <param name="prefabContentsRoot"> The temporary prefab root when successful. </param>
        /// <param name="errorMessage"> The validation error message when acquisition fails. </param>
        /// <returns> <see langword="true" /> when request-local prefab state is available; otherwise <see langword="false" />. </returns>
        internal bool TryGetOrCreateTemporaryPrefabContentsRoot (
            string prefabPath,
            out GameObject? prefabContentsRoot,
            out string errorMessage)
        {
            if (temporaryObjectScope.TryGetTemporaryPrefabContentsRoot(prefabPath, out prefabContentsRoot)
                && prefabContentsRoot != null)
            {
                errorMessage = string.Empty;
                return true;
            }

            if (PrefabOperationUtilities.TryGetOpenedPrefabStage(prefabPath, out var prefabStage, out _))
            {
                var openedPrefabRoot = prefabStage!.prefabContentsRoot;
                if (openedPrefabRoot == null)
                {
                    prefabContentsRoot = null;
                    errorMessage = $"Opened prefab root is not available: {prefabPath}.";
                    return false;
                }

                if (openedPrefabRoot.scene.isDirty)
                {
                    return temporaryObjectScope.TryCloneTemporaryPrefabContentsRootFromOpenedStage(
                        prefabPath,
                        openedPrefabRoot,
                        out prefabContentsRoot,
                        out errorMessage);
                }
            }

            return PrefabOperationUtilities.TryGetOrLoadTemporaryPrefabContentsRoot(
                prefabPath,
                this,
                out prefabContentsRoot,
                out errorMessage);
        }

        /// <summary> Tries to resolve one tracked preview scene back to its logical scene asset path. </summary>
        /// <param name="scene"> The preview scene instance. </param>
        /// <param name="scenePath"> The logical scene asset path when the scene is tracked. </param>
        /// <returns> <see langword="true" /> when <paramref name="scene" /> is one request-local preview scene; otherwise <see langword="false" />. </returns>
        internal bool TryResolveTemporaryScenePath (
            Scene scene,
            out string scenePath)
        {
            return temporarySceneRegistry.TryResolvePreviewScenePath(scene, out scenePath);
        }

        /// <summary> Tracks one temporary object for cleanup at the end of request execution. </summary>
        /// <param name="unityObject"> The temporary object to destroy. </param>
        internal void TrackTemporaryObject (UnityEngine.Object unityObject)
        {
            temporaryObjectScope.TrackTemporaryObject(unityObject);
        }

        /// <summary> Determines whether one Unity object is tracked as request-local temporary state. </summary>
        /// <param name="unityObject"> The Unity object to test. </param>
        /// <returns> <see langword="true" /> when the object belongs to this request-local temporary scope; otherwise <see langword="false" />. </returns>
        internal bool IsTrackedTemporaryObject (UnityEngine.Object unityObject)
        {
            return temporaryObjectScope.ContainsTemporaryObject(unityObject);
        }

        /// <summary> Releases all request-local temporary resources owned by this execution context. </summary>
        public void Dispose ()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            temporarySceneRegistry.Clear();
            temporaryObjectScope.Cleanup();
            temporaryAliasRegistry.Clear();
            componentSandboxRegistry.Clear();
            assetSandboxRegistry.Clear();
            plannedAssetRegistry.Clear();
            requestAttributedChangeRegistry.ClearAll();
            plannedLiveSceneOpenPaths.Clear();
            plannedLivePrefabOpenPaths.Clear();
        }
    }
}
