using System;
using System.Collections.Generic;
using System.Linq;
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

        private readonly Dictionary<string, Dictionary<string, Dictionary<string, PrefabOverridePropertyChange>>> prefabOverridePropertyChanges =
            new Dictionary<string, Dictionary<string, Dictionary<string, PrefabOverridePropertyChange>>>(StringComparer.Ordinal);

        private readonly DeletedGlobalObjectIdRegistry deletedGlobalObjectIdRegistry = new DeletedGlobalObjectIdRegistry();

        private readonly HashSet<string> plannedLiveSceneOpenPaths = new HashSet<string>(StringComparer.Ordinal);

        private readonly HashSet<string> plannedLivePrefabOpenPaths = new HashSet<string>(StringComparer.Ordinal);

        private readonly List<PlannedPrefabCreation> plannedPrefabCreations = new List<PlannedPrefabCreation>();

        private bool disposed;

        /// <summary> Initializes a new instance of the <see cref="OperationExecutionContext" /> class. </summary>
        public OperationExecutionContext ()
        {
        }

        /// <summary> Gets the alias store used to share resolved references within one request. </summary>
        internal OperationAliasStore AliasStore { get; } = new OperationAliasStore();

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
        /// <param name="targetGameObject"> The request-local GameObject that owns the ensured component semantically. </param>
        /// <param name="resource"> The owning resource. </param>
        internal void SetEnsuredComponent (
            string targetGlobalObjectId,
            Type componentType,
            Component component,
            GameObject targetGameObject,
            OperationResource resource)
        {
            componentSandboxRegistry.SetEnsuredComponent(targetGlobalObjectId, componentType, component, targetGameObject, resource);
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

        /// <summary> Collects all plan-time ensured components tracked for one target object. </summary>
        /// <param name="targetGlobalObjectId"> The target tracking key. </param>
        /// <param name="destination"> The destination collection that receives the ensured components. </param>
        internal void CollectEnsuredComponentStates (
            string targetGlobalObjectId,
            ICollection<ComponentSandboxRegistry.EnsuredComponentState> destination)
        {
            componentSandboxRegistry.CollectEnsuredComponentStates(targetGlobalObjectId, destination);
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

        /// <summary> Tries to resolve the Prefab override correlation key for one tracked request-local component. </summary>
        /// <param name="component"> The tracked temporary component. </param>
        /// <param name="targetKey"> The Prefab override correlation key when found. </param>
        /// <returns> <see langword="true" /> when the component belongs to tracked plan-time state; otherwise <see langword="false" />. </returns>
        internal bool TryResolveTrackedComponentTargetKey (
            Component component,
            out string targetKey)
        {
            return componentSandboxRegistry.TryResolveTrackedComponentTargetKey(component, out targetKey);
        }

        /// <summary> Tries to resolve the semantic owner GameObject tracking key for one tracked request-local component. </summary>
        /// <param name="component"> The tracked temporary component. </param>
        /// <param name="ownerKey"> The owner GameObject tracking key when found. </param>
        /// <returns> <see langword="true" /> when the component belongs to tracked plan-time state; otherwise <see langword="false" />. </returns>
        internal bool TryResolveTrackedComponentOwnerKey (
            Component component,
            out string ownerKey)
        {
            return componentSandboxRegistry.TryResolveTrackedComponentOwnerKey(component, out ownerKey);
        }

        /// <summary> Tries to resolve the semantic owner GameObject for one tracked request-local component. </summary>
        /// <param name="component"> The tracked temporary component. </param>
        /// <param name="ownerGameObject"> The owner GameObject when found. </param>
        /// <returns> <see langword="true" /> when the component belongs to tracked plan-time state and its owner still exists; otherwise <see langword="false" />. </returns>
        internal bool TryResolveTrackedComponentOwnerGameObject (
            Component component,
            out GameObject? ownerGameObject)
        {
            return componentSandboxRegistry.TryResolveTrackedComponentOwnerGameObject(component, out ownerGameObject);
        }

        /// <summary> Stores or replaces one temporary component shadow keyed by source GlobalObjectId. </summary>
        /// <param name="globalObjectId"> The source component GlobalObjectId. </param>
        /// <param name="component"> The temporary shadow component. </param>
        /// <param name="sourceComponent"> The source component whose serialized state is represented by <paramref name="component" />. </param>
        /// <param name="ownerGameObject"> The request-local GameObject that semantically owns <paramref name="component" />. </param>
        /// <param name="ownerGameObjectTrackingKey"> The tracking key of <paramref name="ownerGameObject" />. </param>
        /// <param name="resource"> The owning resource. </param>
        internal void SetComponentShadow (
            string globalObjectId,
            Component component,
            Component sourceComponent,
            GameObject ownerGameObject,
            string ownerGameObjectTrackingKey,
            OperationResource resource)
        {
            componentSandboxRegistry.SetComponentShadow(
                globalObjectId,
                component,
                sourceComponent,
                ownerGameObject,
                ownerGameObjectTrackingKey,
                resource,
                temporaryAliasRegistry);
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

        /// <summary> Tries to resolve one mirrored live scene source object to its request-local preview object. </summary>
        /// <param name="scenePath"> The logical scene asset path. </param>
        /// <param name="sourceObject"> The mirrored live source object. </param>
        /// <param name="previewObject"> The preview object when found. </param>
        /// <returns> <see langword="true" /> when the source object originated one dirty loaded-scene mirror; otherwise <see langword="false" />. </returns>
        internal bool TryResolveTemporaryScenePreviewObject (
            string scenePath,
            UnityEngine.Object sourceObject,
            out UnityEngine.Object? previewObject)
        {
            return temporarySceneRegistry.TryResolvePreviewObjectFromSourceObject(scenePath, sourceObject, out previewObject);
        }

        /// <summary> Tries to resolve one request-local preview scene object to its stable GlobalObjectId text. </summary>
        /// <param name="scenePath"> The logical scene asset path. </param>
        /// <param name="previewObject"> The preview object. </param>
        /// <param name="stableReference"> The stable GlobalObjectId text when found. </param>
        /// <returns> <see langword="true" /> when the preview object has one explicit stable-reference mapping; otherwise <see langword="false" />. </returns>
        internal bool TryResolveTemporarySceneStableReference (
            string scenePath,
            UnityEngine.Object previewObject,
            out string stableReference)
        {
            return temporarySceneRegistry.TryResolveStableReferenceFromPreviewObject(scenePath, previewObject, out stableReference);
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

        /// <summary> Tries to resolve one mirrored live prefab object to its request-local preview object. </summary>
        /// <param name="prefabPath"> The logical prefab asset path. </param>
        /// <param name="sourceObject"> The mirrored live source object. </param>
        /// <param name="previewObject"> The preview object when found. </param>
        /// <returns> <see langword="true" /> when the live source object belongs to an opened-stage mirror tracked for the request; otherwise <see langword="false" />. </returns>
        internal bool TryResolveTemporaryPrefabPreviewObject (
            string prefabPath,
            UnityEngine.Object sourceObject,
            out UnityEngine.Object? previewObject)
        {
            return temporaryObjectScope.TryResolvePreviewObjectFromMirroredSourceObject(prefabPath, sourceObject, out previewObject);
        }

        /// <summary> Tries to resolve one request-local preview prefab object to its stable GlobalObjectId text. </summary>
        /// <param name="prefabPath"> The logical prefab asset path. </param>
        /// <param name="previewObject"> The preview object. </param>
        /// <param name="stableReference"> The stable GlobalObjectId text when found. </param>
        /// <returns> <see langword="true" /> when the preview object has one explicit stable-reference mapping; otherwise <see langword="false" />. </returns>
        internal bool TryResolveTemporaryPrefabStableReference (
            string prefabPath,
            UnityEngine.Object previewObject,
            out string stableReference)
        {
            return temporaryObjectScope.TryResolveStableReferenceFromPreviewObject(prefabPath, previewObject, out stableReference);
        }

        /// <summary> Tries to resolve one request-local preview object to the stable tracking key of its source object. </summary>
        /// <param name="unityObject"> The object to normalize. </param>
        /// <param name="resource"> The logical owner resource for the object. </param>
        /// <param name="trackingKey"> The stable tracking key when the object belongs to request-local preview state. </param>
        /// <returns> <see langword="true" /> when the object was normalized from preview state; otherwise <see langword="false" />. </returns>
        internal bool TryResolvePreviewSourceTrackingKey (
            UnityEngine.Object unityObject,
            OperationResource resource,
            out string trackingKey)
        {
            if (unityObject == null)
            {
                throw new ArgumentNullException(nameof(unityObject));
            }

            switch (resource.Kind)
            {
                case OperationTouchKind.Scene:
                    if (TryResolveTemporarySceneStableReference(resource.Path, unityObject, out trackingKey))
                    {
                        return true;
                    }

                    if (TryResolveTemporarySceneSourceObject(resource.Path, unityObject, out var sceneSourceObject)
                        && sceneSourceObject != null)
                    {
                        trackingKey = UnityObjectReferenceResolver.CreateTrackingKey(sceneSourceObject);
                        return true;
                    }

                    break;

                case OperationTouchKind.Prefab:
                    if (TryResolveTemporaryPrefabStableReference(resource.Path, unityObject, out trackingKey))
                    {
                        return true;
                    }

                    if (TryResolveTemporaryPrefabSourceObject(resource.Path, unityObject, out var prefabSourceObject)
                        && prefabSourceObject != null)
                    {
                        trackingKey = UnityObjectReferenceResolver.CreateTrackingKey(prefabSourceObject);
                        return true;
                    }

                    break;
            }

            trackingKey = string.Empty;
            return false;
        }

        /// <summary> Creates the canonical target key used to correlate request-attributed Prefab override property changes. </summary>
        /// <param name="targetReference"> The reference used by the current primitive operation. </param>
        /// <param name="component"> The resolved component target. </param>
        /// <param name="resource"> The logical owner resource for the component. </param>
        /// <returns> A non-empty request-local target key for the component. </returns>
        internal string CreatePrefabOverrideTargetKey (
            UnityObjectReference targetReference,
            Component component,
            OperationResource resource)
        {
            if (component == null)
            {
                throw new ArgumentNullException(nameof(component));
            }

            if (targetReference.Kind == UnityObjectReferenceKind.Selector
                && targetReference.Selector.Kind == ResolveSelectorKind.GlobalObjectId)
            {
                return targetReference.Selector.GlobalObjectId!;
            }

            if (TryResolveTrackedComponentTargetKey(component, out var trackedTargetKey))
            {
                return trackedTargetKey;
            }

            if (TryResolvePreviewSourceTrackingKey(component, resource, out var previewSourceTrackingKey))
            {
                return previewSourceTrackingKey;
            }

            return UnityObjectReferenceResolver.CreateTrackingKey(component);
        }

        /// <summary> Creates the canonical owner GameObject key for a component target. </summary>
        /// <param name="component"> The resolved component target. </param>
        /// <param name="resource"> The logical owner resource for the component. </param>
        /// <returns> A non-empty request-local owner GameObject tracking key. </returns>
        internal string CreateComponentOwnerTrackingKey (
            Component component,
            OperationResource resource)
        {
            if (component == null)
            {
                throw new ArgumentNullException(nameof(component));
            }

            if (TryResolveTrackedComponentOwnerKey(component, out var trackedOwnerKey))
            {
                return trackedOwnerKey;
            }

            if (TryResolvePreviewSourceTrackingKey(component.gameObject, resource, out var previewSourceOwnerKey))
            {
                return previewSourceOwnerKey;
            }

            return UnityObjectReferenceResolver.CreateTrackingKey(component.gameObject);
        }

        /// <summary> Tries to resolve one stable GlobalObjectId text to any request-local preview object. </summary>
        /// <param name="stableReference"> The stable GlobalObjectId text. </param>
        /// <param name="previewObject"> The preview object when found. </param>
        /// <returns> <see langword="true" /> when the stable reference maps into tracked request-local preview state; otherwise <see langword="false" />. </returns>
        internal bool TryResolveTemporaryPreviewObjectFromStableReference (
            string stableReference,
            out UnityEngine.Object? previewObject)
        {
            if (temporarySceneRegistry.TryResolvePreviewObjectFromStableReference(stableReference, out previewObject))
            {
                return true;
            }

            return temporaryObjectScope.TryResolvePreviewObjectFromStableReference(stableReference, out previewObject);
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

        /// <summary> Marks one stable GlobalObjectId as deleted from request-local plan state. </summary>
        /// <param name="globalObjectId"> The deleted stable GlobalObjectId. </param>
        internal void MarkDeletedGlobalObjectId (string globalObjectId)
        {
            deletedGlobalObjectIdRegistry.MarkDeleted(globalObjectId);
        }

        /// <summary> Determines whether one stable GlobalObjectId was deleted from request-local plan state. </summary>
        /// <param name="globalObjectId"> The stable GlobalObjectId. </param>
        /// <returns> <see langword="true" /> when the object was deleted in request-local plan state; otherwise <see langword="false" />. </returns>
        internal bool IsDeletedGlobalObjectId (string globalObjectId)
        {
            return deletedGlobalObjectIdRegistry.Contains(globalObjectId);
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

        /// <summary> Records one request-attributed Prefab instance property change for the current edit step and target. </summary>
        /// <param name="editStepId"> The edit step identifier that scopes apply/revert eligibility. </param>
        /// <param name="targetKey"> The Prefab override target key created from the current target reference and request-local component state. </param>
        /// <param name="propertyPath"> The exact <c>SerializedProperty.propertyPath</c> changed by a preceding set. </param>
        /// <param name="wasPrefabOverrideBeforeRequest"> Whether the property was already a Prefab override before this request changed it. </param>
        /// <param name="valueHashBeforeSet"> The normalized property value hash observed before the set that is being recorded. </param>
        /// <param name="valueHashAfterSet"> The normalized property value hash observed after the set that is being recorded. </param>
        internal void RecordPrefabOverridePropertyChange (
            string editStepId,
            string targetKey,
            string propertyPath,
            bool wasPrefabOverrideBeforeRequest,
            string valueHashBeforeSet,
            string valueHashAfterSet)
        {
            if (!prefabOverridePropertyChanges.TryGetValue(editStepId, out var changesByTarget))
            {
                changesByTarget = new Dictionary<string, Dictionary<string, PrefabOverridePropertyChange>>(StringComparer.Ordinal);
                prefabOverridePropertyChanges.Add(editStepId, changesByTarget);
            }

            if (!changesByTarget.TryGetValue(targetKey, out var changesByPath))
            {
                changesByPath = new Dictionary<string, PrefabOverridePropertyChange>(StringComparer.Ordinal);
                changesByTarget.Add(targetKey, changesByPath);
            }

            var initialValueHash = valueHashBeforeSet;
            if (changesByPath.TryGetValue(propertyPath, out var existingChange))
            {
                wasPrefabOverrideBeforeRequest = existingChange.WasPrefabOverrideBeforeRequest;
                initialValueHash = existingChange.ValueHashBeforeRequest;
            }

            changesByPath[propertyPath] = new PrefabOverridePropertyChange(
                propertyPath,
                wasPrefabOverrideBeforeRequest,
                initialValueHash,
                isEffective: !string.Equals(valueHashAfterSet, initialValueHash, StringComparison.Ordinal));
        }

        /// <summary> Tries to retrieve one previously recorded request-attributed Prefab instance property change. </summary>
        /// <param name="editStepId"> The edit step identifier that scopes apply/revert eligibility. </param>
        /// <param name="targetKey"> The Prefab override target key used when the change was recorded. </param>
        /// <param name="propertyPath"> The exact <c>SerializedProperty.propertyPath</c> to retrieve. </param>
        /// <param name="change"> The recorded change when the method returns <see langword="true" />; otherwise the default value. </param>
        /// <returns> <see langword="true" /> when a change was recorded for the same edit step, target, and property path; otherwise <see langword="false" />. </returns>
        internal bool TryGetPrefabOverridePropertyChange (
            string editStepId,
            string targetKey,
            string propertyPath,
            out PrefabOverridePropertyChange change)
        {
            change = default;
            if (!prefabOverridePropertyChanges.TryGetValue(editStepId, out var changesByTarget)
                || !changesByTarget.TryGetValue(targetKey, out var changesByPath))
            {
                return false;
            }

            return changesByPath.TryGetValue(propertyPath, out change);
        }

        /// <summary> Tries to collect effective request-attributed Prefab instance property changes for one target. </summary>
        /// <param name="editStepId"> The edit step identifier that scopes apply/revert eligibility. </param>
        /// <param name="targetKey"> The Prefab override target key used when changes were recorded. </param>
        /// <param name="requestedPropertyPaths"> The exact property paths requested by the operation, or <see langword="null" /> to select every effective path for the target. </param>
        /// <param name="changes"> The selected effective changes when the method returns <see langword="true" />; otherwise an empty collection. </param>
        /// <param name="errorMessage"> The validation error when the method returns <see langword="false" />; otherwise an empty string. </param>
        /// <returns> <see langword="true" /> when at least one effective change is selected and all requested paths are eligible; otherwise <see langword="false" />. </returns>
        internal bool TryCollectPrefabOverridePropertyChanges (
            string editStepId,
            string targetKey,
            IReadOnlyList<string>? requestedPropertyPaths,
            out IReadOnlyList<PrefabOverridePropertyChange> changes,
            out string errorMessage)
        {
            changes = Array.Empty<PrefabOverridePropertyChange>();
            if (!prefabOverridePropertyChanges.TryGetValue(editStepId, out var changesByTarget)
                || !changesByTarget.TryGetValue(targetKey, out var changesByPath))
            {
                errorMessage = "Prefab override action requires a preceding effective set on the same edit step and current target.";
                return false;
            }

            if (requestedPropertyPaths == null)
            {
                if (!ContainsEffectivePrefabOverridePropertyChange(changesByPath))
                {
                    errorMessage = "Prefab override action requires a preceding effective set on the same edit step and current target.";
                    return false;
                }

                changes = changesByPath.Values
                    .Where(static change => change.IsEffective)
                    .OrderBy(static change => change.PropertyPath, StringComparer.Ordinal)
                    .ToArray();
                errorMessage = string.Empty;
                return true;
            }

            var selectedChanges = new List<PrefabOverridePropertyChange>(requestedPropertyPaths.Count);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < requestedPropertyPaths.Count; i++)
            {
                var propertyPath = requestedPropertyPaths[i];
                if (string.IsNullOrWhiteSpace(propertyPath))
                {
                    errorMessage = "Prefab override property path must not be empty.";
                    return false;
                }

                if (!seen.Add(propertyPath))
                {
                    errorMessage = $"Prefab override property path is duplicated: {propertyPath}.";
                    return false;
                }

                if (!changesByPath.TryGetValue(propertyPath, out var change))
                {
                    errorMessage = $"Prefab override property path was not changed by a preceding effective set on the same edit step and current target: {propertyPath}.";
                    return false;
                }

                if (!change.IsEffective)
                {
                    errorMessage = $"Prefab override property path was returned to its pre-request value by a later set on the same edit step and current target: {propertyPath}.";
                    return false;
                }

                selectedChanges.Add(change);
            }

            if (selectedChanges.Count == 0)
            {
                errorMessage = "Prefab override propertyPaths must contain at least one path when specified.";
                return false;
            }

            changes = selectedChanges;
            errorMessage = string.Empty;
            return true;
        }

        private static bool ContainsEffectivePrefabOverridePropertyChange (
            IReadOnlyDictionary<string, PrefabOverridePropertyChange> changesByPath)
        {
            foreach (var pair in changesByPath)
            {
                if (pair.Value.IsEffective)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary> Copies all request-attributed resources tracked for the current request into the destination collection. </summary>
        /// <param name="destination"> The destination collection that receives tracked resources. </param>
        internal void CopyRequestAttributedChangesTo (ICollection<OperationResource> destination)
        {
            requestAttributedChangeRegistry.CopyTo(destination);
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

        /// <summary> Tracks that one prior step planned to create a Prefab asset from the specified scene root. </summary>
        /// <param name="sourceRoot"> The scene root that will become a Prefab instance when call execution reaches the create operation. </param>
        /// <param name="prefabPath"> The Prefab asset path reserved by the create operation. </param>
        /// <exception cref="ArgumentException"> <paramref name="prefabPath" /> is null, empty, or whitespace. </exception>
        /// <exception cref="ArgumentNullException"> <paramref name="sourceRoot" /> is <see langword="null" />. </exception>
        internal void TrackPlannedPrefabCreation (
            GameObject sourceRoot,
            string prefabPath)
        {
            if (sourceRoot == null)
            {
                throw new ArgumentNullException(nameof(sourceRoot));
            }

            if (string.IsNullOrWhiteSpace(prefabPath))
            {
                throw new ArgumentException("Prefab path must not be null, empty, or whitespace.", nameof(prefabPath));
            }

            var sourceGameObjectKeys = new HashSet<string>(StringComparer.Ordinal);
            CollectHierarchyTrackingKeys(sourceRoot, sourceGameObjectKeys);
            plannedPrefabCreations.Add(new PlannedPrefabCreation(sourceRoot, prefabPath, sourceGameObjectKeys));
        }

        /// <summary> Determines whether a scene Component belongs to a Prefab asset planned earlier in this request. </summary>
        /// <param name="component"> The candidate scene component. </param>
        /// <param name="prefabPath"> The expected Prefab asset path. </param>
        /// <returns> <see langword="true" /> when the component is owned by a planned Prefab creation root for <paramref name="prefabPath" />; otherwise <see langword="false" />. </returns>
        /// <exception cref="ArgumentNullException"> <paramref name="component" /> is <see langword="null" />. </exception>
        internal bool IsPlannedPrefabInstanceLineage (
            Component component,
            string prefabPath)
        {
            if (component == null)
            {
                throw new ArgumentNullException(nameof(component));
            }

            return IsPlannedPrefabInstanceLineage(component, prefabPath, requirePrefabPath: true);
        }

        /// <summary> Determines whether a scene Component belongs to any Prefab creation planned earlier in this request. </summary>
        /// <param name="component"> The candidate scene component. </param>
        /// <returns> <see langword="true" /> when the component is owned by a planned Prefab creation root; otherwise <see langword="false" />. </returns>
        /// <exception cref="ArgumentNullException"> <paramref name="component" /> is <see langword="null" />. </exception>
        internal bool IsPlannedPrefabInstanceLineage (Component component)
        {
            if (component == null)
            {
                throw new ArgumentNullException(nameof(component));
            }

            return IsPlannedPrefabInstanceLineage(component, prefabPath: null, requirePrefabPath: false);
        }

        private bool IsPlannedPrefabInstanceLineage (
            Component component,
            string? prefabPath,
            bool requirePrefabPath)
        {
            if (requirePrefabPath && string.IsNullOrWhiteSpace(prefabPath))
            {
                return false;
            }

            if (IsGameObjectInsidePlannedPrefabCreation(component.gameObject, prefabPath))
            {
                return true;
            }

            return TryResolveTrackedComponentOwnerGameObject(component, out var ownerGameObject)
                   && ownerGameObject != null
                   && IsGameObjectInsidePlannedPrefabCreation(ownerGameObject, prefabPath);
        }

        private bool IsGameObjectInsidePlannedPrefabCreation (
            GameObject gameObject,
            string? prefabPath)
        {
            if (gameObject == null)
            {
                throw new ArgumentNullException(nameof(gameObject));
            }

            for (var i = 0; i < plannedPrefabCreations.Count; i++)
            {
                var creation = plannedPrefabCreations[i];
                if (creation.SourceRoot == null)
                {
                    continue;
                }

                if (prefabPath != null && !string.Equals(creation.PrefabPath, prefabPath, StringComparison.Ordinal))
                {
                    continue;
                }

                var gameObjectKey = UnityObjectReferenceResolver.CreateTrackingKey(gameObject);
                if (creation.SourceGameObjectKeys.Contains(gameObjectKey)
                    && (gameObject == creation.SourceRoot || gameObject.transform.IsChildOf(creation.SourceRoot.transform)))
                {
                    return true;
                }
            }

            return false;
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

        /// <summary> Collects current live temporary asset-shadow states. Destroyed shadow objects are omitted. </summary>
        /// <param name="destination"> The destination collection that receives current asset-shadow states. </param>
        /// <exception cref="ArgumentNullException"> <paramref name="destination" /> is <see langword="null" />. </exception>
        internal void CollectAssetShadowStates (ICollection<AssetSandboxRegistry.AssetShadowState> destination)
        {
            assetSandboxRegistry.CollectAssetShadowStates(destination);
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

        /// <summary> Collects current live plan-time created asset states. Destroyed planned objects are omitted. </summary>
        /// <param name="destination"> The destination collection that receives current planned-asset states. </param>
        /// <exception cref="ArgumentNullException"> <paramref name="destination" /> is <see langword="null" />. </exception>
        internal void CollectPlannedAssetStates (ICollection<PlannedAssetRegistry.PlannedAssetState> destination)
        {
            plannedAssetRegistry.CollectPlannedAssetStates(destination);
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

        /// <summary> Releases one request-local preview scene when it was created speculatively for the current request. </summary>
        /// <param name="scenePath"> The logical scene path to release. </param>
        internal void ReleaseTemporaryScene (string scenePath)
        {
            _ = temporarySceneRegistry.ReleasePreviewScene(scenePath);
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

        /// <summary> Releases one request-local temporary prefab execution session. </summary>
        /// <param name="prefabPath"> The prefab asset path to release. </param>
        internal void ReleaseTemporaryPrefabExecutionSession (string prefabPath)
        {
            _ = temporaryObjectScope.ReleaseTemporaryPrefabContentsRoot(prefabPath);
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
            prefabOverridePropertyChanges.Clear();
            deletedGlobalObjectIdRegistry.Clear();
            plannedLiveSceneOpenPaths.Clear();
            plannedLivePrefabOpenPaths.Clear();
            plannedPrefabCreations.Clear();
        }

        /// <summary> Describes one request-attributed Prefab instance property change. </summary>
        internal readonly struct PrefabOverridePropertyChange
        {
            /// <summary> Initializes a new instance of the <see cref="PrefabOverridePropertyChange" /> struct. </summary>
            /// <param name="propertyPath"> The exact <c>SerializedProperty.propertyPath</c> changed by a preceding set. </param>
            /// <param name="wasPrefabOverrideBeforeRequest"> Whether the property was already a Prefab override before this request changed it. </param>
            /// <param name="valueHashBeforeRequest"> The normalized property value hash captured before the first request-attributed set for this path. </param>
            /// <param name="isEffective"> Whether the latest request-attributed value differs from <paramref name="valueHashBeforeRequest" />. </param>
            public PrefabOverridePropertyChange (
                string propertyPath,
                bool wasPrefabOverrideBeforeRequest,
                string valueHashBeforeRequest,
                bool isEffective)
            {
                PropertyPath = propertyPath;
                WasPrefabOverrideBeforeRequest = wasPrefabOverrideBeforeRequest;
                ValueHashBeforeRequest = valueHashBeforeRequest;
                IsEffective = isEffective;
            }

            /// <summary> Gets the exact <c>SerializedProperty.propertyPath</c> changed by a preceding set. </summary>
            public string PropertyPath { get; }

            /// <summary> Gets a value indicating whether the property was already a Prefab override before this request changed it. </summary>
            public bool WasPrefabOverrideBeforeRequest { get; }

            /// <summary> Gets the normalized property value hash captured before the first request-attributed set for this path. </summary>
            public string ValueHashBeforeRequest { get; }

            /// <summary> Gets a value indicating whether the latest request-attributed value differs from the pre-request value. </summary>
            public bool IsEffective { get; }
        }

        private readonly struct PlannedPrefabCreation
        {
            public PlannedPrefabCreation (
                GameObject sourceRoot,
                string prefabPath,
                HashSet<string> sourceGameObjectKeys)
            {
                SourceRoot = sourceRoot;
                PrefabPath = prefabPath;
                SourceGameObjectKeys = sourceGameObjectKeys;
            }

            public GameObject SourceRoot { get; }

            public string PrefabPath { get; }

            public HashSet<string> SourceGameObjectKeys { get; }
        }

        private static void CollectHierarchyTrackingKeys (
            GameObject root,
            ICollection<string> destination)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            destination.Add(UnityObjectReferenceResolver.CreateTrackingKey(root));
            var transform = root.transform;
            for (var i = 0; i < transform.childCount; i++)
            {
                CollectHierarchyTrackingKeys(transform.GetChild(i).gameObject, destination);
            }
        }
    }
}
