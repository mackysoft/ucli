using System;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one per-request execution context shared by operation phases. </summary>
    public sealed class OperationExecutionContext
    {
        private readonly TemporaryAliasRegistry temporaryAliasRegistry = new TemporaryAliasRegistry();

        private readonly ComponentSandboxRegistry componentSandboxRegistry = new ComponentSandboxRegistry();

        private readonly AssetSandboxRegistry assetSandboxRegistry = new AssetSandboxRegistry();

        private readonly PlannedAssetRegistry plannedAssetRegistry = new PlannedAssetRegistry();

        private readonly TemporaryObjectScope temporaryObjectScope = new TemporaryObjectScope();

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
        /// <param name="scenePath"> The logical resource path associated with the temporary object. </param>
        internal void SetTemporaryAlias (
            string alias,
            UnityEngine.Object unityObject,
            string scenePath,
            string? sourceGlobalObjectId = null)
        {
            SetTemporaryAlias(
                alias,
                unityObject,
                new OperationResource(OperationTouchKind.Scene, scenePath),
                sourceGlobalObjectId);
        }

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
        /// <param name="scenePath"> The owning resource path. </param>
        internal void SetEnsuredComponent (
            string targetGlobalObjectId,
            Type componentType,
            Component component,
            string scenePath)
        {
            SetEnsuredComponent(
                targetGlobalObjectId,
                componentType,
                component,
                new OperationResource(OperationTouchKind.Scene, scenePath));
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

        /// <summary> Stores or replaces one temporary component shadow keyed by source GlobalObjectId. </summary>
        /// <param name="globalObjectId"> The source component GlobalObjectId. </param>
        /// <param name="component"> The temporary shadow component. </param>
        /// <param name="scenePath"> The owning resource path. </param>
        internal void SetComponentShadow (
            string globalObjectId,
            Component component,
            string scenePath)
        {
            SetComponentShadow(
                globalObjectId,
                component,
                new OperationResource(OperationTouchKind.Scene, scenePath));
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
        /// <param name="scenePath"> The owning resource path. </param>
        internal void ReplaceTrackedTemporaryComponent (
            Component sourceComponent,
            Component replacementComponent,
            string scenePath)
        {
            ReplaceTrackedTemporaryComponent(
                sourceComponent,
                replacementComponent,
                new OperationResource(OperationTouchKind.Scene, scenePath));
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
        /// <param name="ownerOperationId"> The operation id that owns the reservation. </param>
        /// <param name="unityObject"> The current temporary asset instance. </param>
        internal void SetPlannedAsset (
            string assetPath,
            string ownerOperationId,
            UnityEngine.Object unityObject)
        {
            plannedAssetRegistry.SetPlannedAsset(assetPath, ownerOperationId, unityObject, temporaryAliasRegistry);
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

        /// <summary> Tracks one temporary object for cleanup at the end of request execution. </summary>
        /// <param name="unityObject"> The temporary object to destroy. </param>
        internal void TrackTemporaryObject (UnityEngine.Object unityObject)
        {
            temporaryObjectScope.TrackTemporaryObject(unityObject);
        }

        /// <summary> Destroys all tracked temporary objects and clears temporary state. </summary>
        internal void CleanupTemporaryObjects ()
        {
            temporaryObjectScope.Cleanup();
            temporaryAliasRegistry.Clear();
            componentSandboxRegistry.Clear();
            assetSandboxRegistry.Clear();
            plannedAssetRegistry.Clear();
        }
    }
}
