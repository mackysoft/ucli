using System;
using System.Collections.Generic;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;
using UnityEditor;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Resolves Prefab override apply/revert targets against the current edit-step scope. </summary>
    internal static class PrefabOverrideResolution
    {
        /// <summary> Resolves a target for applying request-attributed Prefab overrides. </summary>
        /// <param name="operation"> The normalized operation whose identifier scopes preceding set operations. </param>
        /// <param name="args"> The apply operation arguments containing the target component, target Prefab asset path, and optional property paths. </param>
        /// <param name="executionContext"> The per-request execution context containing request-attributed property changes. </param>
        /// <param name="allowTemporaryState"> <see langword="true" /> to allow plan-time temporary state, including same-request planned Prefab creation. </param>
        /// <param name="state"> The resolved apply state when the method returns <see langword="true" />; otherwise the default value. </param>
        /// <param name="errorMessage"> The validation error when the method returns <see langword="false" />; otherwise an empty string. </param>
        /// <returns> <see langword="true" /> when the target is a valid scene component target and at least one requested effective change can be applied; otherwise <see langword="false" />. </returns>
        public static bool TryResolveForApply (
            NormalizedOperation operation,
            PrefabOverrideArgs args,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out State state,
            out string errorMessage)
        {
            return TryResolve(
                operation,
                args,
                executionContext,
                allowTemporaryState,
                rejectPreRequestOverrides: false,
                out state,
                out errorMessage);
        }

        /// <summary> Resolves a target for reverting request-attributed Prefab overrides. </summary>
        /// <param name="operation"> The normalized operation whose identifier scopes preceding set operations. </param>
        /// <param name="args"> The revert operation arguments containing the target component, target Prefab asset path, and optional property paths. </param>
        /// <param name="executionContext"> The per-request execution context containing request-attributed property changes. </param>
        /// <param name="allowTemporaryState"> <see langword="true" /> to allow plan-time temporary state, including same-request planned Prefab creation. </param>
        /// <param name="state"> The resolved revert state when the method returns <see langword="true" />; otherwise the default value. </param>
        /// <param name="errorMessage"> The validation error when the method returns <see langword="false" />; otherwise an empty string. </param>
        /// <returns> <see langword="true" /> when the target is a valid scene component target and every requested effective change was created by the current request; otherwise <see langword="false" />. </returns>
        public static bool TryResolveForRevert (
            NormalizedOperation operation,
            PrefabOverrideArgs args,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out State state,
            out string errorMessage)
        {
            return TryResolve(
                operation,
                args,
                executionContext,
                allowTemporaryState,
                rejectPreRequestOverrides: true,
                out state,
                out errorMessage);
        }

        private static bool TryResolve (
            NormalizedOperation operation,
            PrefabOverrideArgs args,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            bool rejectPreRequestOverrides,
            out State state,
            out string errorMessage)
        {
            state = default;
            if (!UnityObjectReferenceContractMapper.TryMap(args.Target, "args.target", out var targetReference, out errorMessage))
            {
                return false;
            }

            if (!TryResolveComponentTarget(
                    operation,
                    targetReference,
                    executionContext,
                    allowTemporaryState,
                    out var componentResolution,
                    out errorMessage))
            {
                return false;
            }

            var component = componentResolution.Component!;
            if (componentResolution.Resource.Kind != OperationTouchKind.Scene)
            {
                errorMessage = "Prefab override actions require a scene component target.";
                return false;
            }

            var targetAssetPath = args.TargetAssetPath.Value;

            var usesPlannedPrefabCreation = allowTemporaryState
                && executionContext.IsPlannedPrefabInstanceLineage(component, targetAssetPath);
            if (!PrefabOperationUtilities.TryEnsurePrefabAssetExists(args.TargetAssetPath, out var prefabAssetErrorMessage))
            {
                if (!usesPlannedPrefabCreation)
                {
                    errorMessage = prefabAssetErrorMessage;
                    return false;
                }
            }

            if (!TryCreateRequestedPropertyPaths(args.PropertyPaths, out var requestedPropertyPaths, out errorMessage))
            {
                return false;
            }

            var targetKey = executionContext.CreateComponentTrackingKey(component, componentResolution.Resource);
            if (!executionContext.TryCollectPrefabOverridePropertyChanges(
                    operation.Id,
                    targetKey,
                    requestedPropertyPaths,
                    out var changes,
                    out errorMessage))
            {
                return false;
            }

            if (rejectPreRequestOverrides
                && !TryRejectPreRequestOverrides(changes, out errorMessage))
            {
                return false;
            }

            var requiresExplicitPrefabAssetMutation = false;
            string? explicitPrefabAssetHierarchyPath = null;
            if (!usesPlannedPrefabCreation)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(component))
                {
                    if (PrefabUtility.GetCorrespondingObjectFromSourceAtPath(component, targetAssetPath) == null)
                    {
                        errorMessage = $"Prefab override target asset is not in the target instance lineage: {targetAssetPath}.";
                        return false;
                    }
                }
                else if (HasRequestAttributedExplicitPrefabAssetMutation(changes))
                {
                    if (!TryResolveRequestAttributedExplicitPrefabAssetTargetPath(
                            component,
                            targetReference,
                            targetAssetPath,
                            changes,
                            executionContext,
                            rejectPreRequestOverrides,
                            out explicitPrefabAssetHierarchyPath,
                            out errorMessage))
                    {
                        return false;
                    }

                    requiresExplicitPrefabAssetMutation = true;
                }
                else
                {
                    errorMessage = "Prefab override actions require a Prefab instance component target.";
                    return false;
                }
            }

            if (!TryValidateProperties(component, changes, out errorMessage))
            {
                return false;
            }

            state = new State(
                component,
                componentResolution.Resource,
                targetAssetPath,
                changes,
                requiresExplicitPrefabAssetMutation,
                explicitPrefabAssetHierarchyPath);
            return true;
        }

        private static bool TryCreateRequestedPropertyPaths (
            IReadOnlyList<SerializedPropertyPath>? propertyPaths,
            out IReadOnlyList<string>? requestedPropertyPaths,
            out string errorMessage)
        {
            requestedPropertyPaths = null;
            if (propertyPaths == null)
            {
                errorMessage = string.Empty;
                return true;
            }

            var values = new string[propertyPaths.Count];
            for (var i = 0; i < propertyPaths.Count; i++)
            {
                var propertyPath = propertyPaths[i];
                if (propertyPath == null)
                {
                    errorMessage = $"Prefab override propertyPaths[{i}] must be a string.";
                    return false;
                }

                values[i] = propertyPath.Value;
            }

            requestedPropertyPaths = values;
            errorMessage = string.Empty;
            return true;
        }

        private static bool TryResolveComponentTarget (
            NormalizedOperation operation,
            UnityObjectReference targetReference,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out ComponentOperationUtilities.ComponentResolutionState componentResolution,
            out string errorMessage)
        {
            if (!ComponentOperationUtilities.TryResolveComponent(
                    targetReference,
                    executionContext,
                    OperationObjectReferenceUtilities.GetReferenceResolutionPolicy(operation, allowTemporaryState),
                    out componentResolution,
                    out errorMessage))
            {
                return false;
            }

            if (allowTemporaryState
                && componentResolution.Component != null
                && !IsPrefabInstanceOrPlannedLineage(componentResolution.Component, executionContext))
            {
                var hasShadowSource = TryResolveComponentShadowSource(
                    componentResolution,
                    executionContext,
                    out var shadowSourceResolution);
                if (hasShadowSource
                    && shadowSourceResolution.Component != null
                    && IsPrefabInstanceOrPlannedLineage(shadowSourceResolution.Component, executionContext))
                {
                    componentResolution = shadowSourceResolution;
                }
                else if (TryResolveLiveComponentTarget(
                    targetReference,
                    componentResolution.TemporaryAliasSourceTrackingKey,
                    executionContext,
                    out var liveComponentResolution))
                {
                    componentResolution = liveComponentResolution;
                }
                else if (hasShadowSource)
                {
                    componentResolution = shadowSourceResolution;
                }
            }

            errorMessage = string.Empty;
            return true;
        }

        private static bool TryResolveComponentShadowSource (
            ComponentOperationUtilities.ComponentResolutionState componentResolution,
            OperationExecutionContext executionContext,
            out ComponentOperationUtilities.ComponentResolutionState sourceComponentResolution)
        {
            sourceComponentResolution = default;
            var component = componentResolution.Component;
            if (component == null)
            {
                return false;
            }

            var sourceTrackingKey = executionContext.CreateComponentTrackingKey(component, componentResolution.Resource);
            if (!executionContext.TryGetComponentShadowState(sourceTrackingKey, out var componentShadowState)
                || componentShadowState.SourceComponent == null
                || !OperationResourceUtilities.TryResolveOwnerResource(
                    componentShadowState.SourceComponent,
                    executionContext,
                    out var sourceResource,
                    out _))
            {
                return false;
            }

            sourceComponentResolution = new ComponentOperationUtilities.ComponentResolutionState(
                componentShadowState.SourceComponent,
                sourceResource,
                componentResolution.TemporaryAliasSourceTrackingKey);
            return true;
        }

        private static bool IsPrefabInstanceOrPlannedLineage (
            Component component,
            OperationExecutionContext executionContext)
        {
            return PrefabUtility.IsPartOfPrefabInstance(component)
                || executionContext.IsPlannedPrefabInstanceLineage(component);
        }

        private static bool TryResolveLiveComponentTarget (
            UnityObjectReference targetReference,
            RequestLocalObjectIdentity? temporaryAliasSourceTrackingKey,
            OperationExecutionContext executionContext,
            out ComponentOperationUtilities.ComponentResolutionState componentResolution)
        {
            componentResolution = default;
            UnityEngine.Object? unityObject;
            if (temporaryAliasSourceTrackingKey != null)
            {
                if (temporaryAliasSourceTrackingKey.TryGetStableGlobalObjectId(out var globalObjectId))
                {
                    if (!ResolveReferenceResolver.TryResolveUnityObject(
                        ResolveSelector.FromGlobalObjectId(globalObjectId),
                        executionContext,
                        allowTemporaryState: false,
                        out unityObject,
                        out _))
                    {
                        return false;
                    }
                }
                else if (!temporaryAliasSourceTrackingKey.TryGetTransientUnityObject(out unityObject))
                {
                    return false;
                }
            }
            else
            {
                if (targetReference.Kind == UnityObjectReferenceKind.Alias
                    || !UnityObjectReferenceResolver.TryResolve(
                        targetReference,
                        executionContext,
                        allowTemporaryState: false,
                        out unityObject,
                        out _))
                {
                    return false;
                }
            }

            if (!(unityObject is Component component)
                || !OperationResourceUtilities.TryResolveOwnerResource(
                    component,
                    executionContext,
                    out var resource,
                    out _))
            {
                return false;
            }

            componentResolution = new ComponentOperationUtilities.ComponentResolutionState(
                component,
                resource,
                temporaryAliasSourceTrackingKey: null);
            return true;
        }

        /// <summary> Loads the explicit Prefab asset target selected for a Play Mode live scene override mutation. </summary>
        /// <param name="state"> The resolved Prefab override state. </param>
        /// <param name="prefabRoot"> The loaded Prefab contents root. The caller must unload it with <see cref="PrefabUtility.UnloadPrefabContents" />. </param>
        /// <param name="assetComponent"> The component inside <paramref name="prefabRoot" /> that corresponds to <see cref="State.Component" />. </param>
        /// <param name="errorMessage"> The validation error when the method returns <see langword="false" />; otherwise an empty string. </param>
        /// <returns> <see langword="true" /> when the explicit Prefab asset target can be loaded and resolved; otherwise <see langword="false" />. </returns>
        internal static bool TryLoadExplicitPrefabAssetTarget (
            State state,
            out GameObject? prefabRoot,
            out Component? assetComponent,
            out string errorMessage)
        {
            prefabRoot = null;
            assetComponent = null;
            if (!state.RequiresExplicitPrefabAssetMutation
                || string.IsNullOrEmpty(state.ExplicitPrefabAssetHierarchyPath))
            {
                errorMessage = "Prefab override action does not use an explicit Prefab asset mutation target.";
                return false;
            }

            if (!TryLoadPrefabContents(state.TargetAssetPath, out prefabRoot, out errorMessage))
            {
                return false;
            }

            if (!TryResolveExplicitPrefabAssetComponentByPath(
                    prefabRoot!,
                    state.ExplicitPrefabAssetHierarchyPath!,
                    state.Component.GetType(),
                    out assetComponent,
                    out errorMessage))
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot!);
                prefabRoot = null;
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        private static bool HasRequestAttributedExplicitPrefabAssetMutation (
            IReadOnlyList<OperationExecutionContext.PrefabOverridePropertyChange> changes)
        {
            for (var i = 0; i < changes.Count; i++)
            {
                if (changes[i].RequiresExplicitPrefabAssetMutation)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolveRequestAttributedExplicitPrefabAssetTargetPath (
            Component component,
            UnityObjectReference targetReference,
            string targetAssetPath,
            IReadOnlyList<OperationExecutionContext.PrefabOverridePropertyChange> changes,
            OperationExecutionContext executionContext,
            bool rejectPreRequestOverrides,
            out string? prefabHierarchyPath,
            out string errorMessage)
        {
            var hasPlannedPrefabLineage = executionContext.IsPlannedPrefabInstanceLineage(component, targetAssetPath);
            if (!hasPlannedPrefabLineage && !EditorApplication.isPlaying)
            {
                prefabHierarchyPath = null;
                errorMessage = $"Prefab override target asset is not in the target instance lineage: {targetAssetPath}.";
                return false;
            }

            // NOTE: Reaching this resolver already requires same-step effective set changes marked as
            // explicit Prefab asset candidates. Play Mode may hide PrefabUtility instance linkage, so the
            // requested asset path is validated by loading the Prefab and matching the component before any copy.
            return TryResolveExplicitPrefabAssetTargetPath(
                component,
                targetReference,
                targetAssetPath,
                changes,
                executionContext,
                rejectPreRequestOverrides,
                out prefabHierarchyPath,
                out errorMessage);
        }

        private static bool TryResolveExplicitPrefabAssetTargetPath (
            Component component,
            UnityObjectReference targetReference,
            string targetAssetPath,
            IReadOnlyList<OperationExecutionContext.PrefabOverridePropertyChange> changes,
            OperationExecutionContext executionContext,
            bool rejectPreRequestOverrides,
            out string? prefabHierarchyPath,
            out string errorMessage)
        {
            prefabHierarchyPath = null;
            if (!TryLoadPrefabContents(targetAssetPath, out var prefabRoot, out errorMessage))
            {
                return false;
            }

            try
            {
                if (!TryResolveExplicitPrefabAssetTargetInLoadedContents(
                        prefabRoot!,
                        component,
                        targetReference,
                        out prefabHierarchyPath,
                        out var assetComponent,
                        out errorMessage))
                {
                    return false;
                }

                if (!TryValidateProperties(assetComponent!, changes, out errorMessage))
                {
                    return false;
                }

                if (rejectPreRequestOverrides
                    && !TryRejectPreRequestOverridesAgainstAsset(
                        assetComponent!,
                        targetAssetPath,
                        changes,
                        executionContext,
                        out errorMessage))
                {
                    return false;
                }

                errorMessage = string.Empty;
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot!);
            }
        }

        private static bool TryLoadPrefabContents (
            string targetAssetPath,
            out GameObject? prefabRoot,
            out string errorMessage)
        {
            prefabRoot = null;
            try
            {
                prefabRoot = PrefabUtility.LoadPrefabContents(targetAssetPath);
            }
            catch (Exception exception)
            {
                errorMessage = $"Prefab asset could not be loaded for override resolution: {targetAssetPath}. {exception.Message}";
                return false;
            }

            if (prefabRoot == null)
            {
                errorMessage = $"Prefab asset could not be loaded for override resolution: {targetAssetPath}.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        private static bool TryResolveExplicitPrefabAssetTargetInLoadedContents (
            GameObject prefabRoot,
            Component component,
            UnityObjectReference targetReference,
            out string? prefabHierarchyPath,
            out Component? assetComponent,
            out string errorMessage)
        {
            prefabHierarchyPath = null;
            assetComponent = null;
            if (!TryGetSceneHierarchyPath(targetReference, component, out var sceneHierarchyPath, out errorMessage))
            {
                return false;
            }

            if (!TryCreateExplicitPrefabHierarchyPathCandidates(sceneHierarchyPath, prefabRoot.name, out var candidates, out errorMessage))
            {
                return false;
            }

            var componentType = component.GetType();
            string? matchedHierarchyPath = null;
            Component? matchedComponent = null;
            for (var i = 0; i < candidates.Count; i++)
            {
                if (!PrefabHierarchyPathResolver.TryResolve(prefabRoot, candidates[i], out var candidateGameObject, out _)
                    || !TryResolveSingleComponentByType(candidateGameObject!, componentType, out var candidateComponent, out _))
                {
                    continue;
                }

                if (matchedComponent != null
                    && matchedComponent != candidateComponent)
                {
                    errorMessage = $"Prefab override target asset hierarchy is ambiguous for '{sceneHierarchyPath}' in Prefab root '{prefabRoot.name}'.";
                    return false;
                }

                matchedHierarchyPath = candidates[i];
                matchedComponent = candidateComponent;
            }

            if (matchedComponent == null)
            {
                errorMessage = $"Prefab override target asset is not in the target hierarchy: {sceneHierarchyPath}.";
                return false;
            }

            prefabHierarchyPath = matchedHierarchyPath;
            assetComponent = matchedComponent;
            errorMessage = string.Empty;
            return true;
        }

        private static bool TryResolveExplicitPrefabAssetComponentByPath (
            GameObject prefabRoot,
            string prefabHierarchyPath,
            Type componentType,
            out Component? assetComponent,
            out string errorMessage)
        {
            assetComponent = null;
            if (!PrefabHierarchyPathResolver.TryResolve(prefabRoot, prefabHierarchyPath, out var gameObject, out errorMessage))
            {
                return false;
            }

            return TryResolveSingleComponentByType(gameObject!, componentType, out assetComponent, out errorMessage);
        }

        private static bool TryResolveSingleComponentByType (
            GameObject gameObject,
            Type componentType,
            out Component? component,
            out string errorMessage)
        {
            component = null;
            var components = gameObject.GetComponents(componentType);
            if (components.Length == 0)
            {
                errorMessage = $"Prefab asset target component was not found: {componentType.FullName}.";
                return false;
            }

            if (components.Length > 1)
            {
                errorMessage = $"Prefab asset target component resolved to multiple components: {componentType.FullName}.";
                return false;
            }

            component = components[0];
            errorMessage = string.Empty;
            return true;
        }

        private static bool TryGetSceneHierarchyPath (
            UnityObjectReference targetReference,
            Component component,
            out string hierarchyPath,
            out string errorMessage)
        {
            if (targetReference.Kind == UnityObjectReferenceKind.Selector
                && !string.IsNullOrEmpty(targetReference.Selector.HierarchyPath))
            {
                hierarchyPath = targetReference.Selector.HierarchyPath!;
                errorMessage = string.Empty;
                return true;
            }

            if (component.gameObject == null)
            {
                hierarchyPath = string.Empty;
                errorMessage = "Prefab override target component is not attached to a GameObject.";
                return false;
            }

            hierarchyPath = CreateHierarchyPath(component.gameObject);
            errorMessage = string.Empty;
            return true;
        }

        private static string CreateHierarchyPath (GameObject gameObject)
        {
            var names = new List<string>();
            var current = gameObject.transform;
            while (current != null)
            {
                names.Add(current.name);
                current = current.parent;
            }

            names.Reverse();
            return string.Join("/", names);
        }

        private static bool TryCreateExplicitPrefabHierarchyPathCandidates (
            string sceneHierarchyPath,
            string prefabRootName,
            out IReadOnlyList<string> candidates,
            out string errorMessage)
        {
            candidates = Array.Empty<string>();
            var segments = sceneHierarchyPath.Split('/');
            if (segments.Length == 0)
            {
                errorMessage = $"Hierarchy path is invalid: {sceneHierarchyPath}.";
                return false;
            }

            var values = new List<string>(segments.Length * 2);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < segments.Length; i++)
            {
                if (segments[i].Length == 0)
                {
                    errorMessage = $"Hierarchy path contains empty segment: {sceneHierarchyPath}.";
                    return false;
                }

                if (string.Equals(segments[i], prefabRootName, StringComparison.Ordinal))
                {
                    AddCandidate(JoinSegments(segments, i), values, seen);
                }

                var replacedRootPath = i + 1 == segments.Length
                    ? prefabRootName
                    : prefabRootName + "/" + JoinSegments(segments, i + 1);
                AddCandidate(replacedRootPath, values, seen);
            }

            candidates = values;
            errorMessage = string.Empty;
            return true;
        }

        private static void AddCandidate (
            string candidate,
            ICollection<string> candidates,
            ISet<string> seen)
        {
            if (seen.Add(candidate))
            {
                candidates.Add(candidate);
            }
        }

        private static string JoinSegments (
            string[] segments,
            int startIndex)
        {
            return string.Join("/", segments, startIndex, segments.Length - startIndex);
        }

        private static bool TryRejectPreRequestOverridesAgainstAsset (
            Component assetComponent,
            string targetAssetPath,
            IReadOnlyList<OperationExecutionContext.PrefabOverridePropertyChange> changes,
            OperationExecutionContext executionContext,
            out string errorMessage)
        {
            var serializedObject = new SerializedObject(assetComponent);
            serializedObject.UpdateIfRequiredOrScript();
            var assetResource = new OperationResource(OperationTouchKind.Prefab, targetAssetPath);
            for (var i = 0; i < changes.Count; i++)
            {
                var propertyPath = changes[i].PropertyPath;
                var property = serializedObject.FindProperty(propertyPath);
                if (property == null)
                {
                    errorMessage = $"SerializedProperty path was not found: {propertyPath}.";
                    return false;
                }

                var assetValueHash = SerializedPropertyValueHasher.Create(property, executionContext, assetResource);
                if (!string.Equals(assetValueHash, changes[i].ValueHashBeforeRequest, StringComparison.Ordinal))
                {
                    errorMessage = $"Prefab override property already existed before the request: {propertyPath}.";
                    return false;
                }
            }

            errorMessage = string.Empty;
            return true;
        }

        private static bool TryRejectPreRequestOverrides (
            IReadOnlyList<OperationExecutionContext.PrefabOverridePropertyChange> changes,
            out string errorMessage)
        {
            for (var i = 0; i < changes.Count; i++)
            {
                if (changes[i].WasPrefabOverrideBeforeRequest)
                {
                    errorMessage = $"Prefab override property already existed before the request: {changes[i].PropertyPath}.";
                    return false;
                }
            }

            errorMessage = string.Empty;
            return true;
        }

        private static bool TryValidateProperties (
            Component component,
            IReadOnlyList<OperationExecutionContext.PrefabOverridePropertyChange> changes,
            out string errorMessage)
        {
            var serializedObject = new SerializedObject(component);
            serializedObject.UpdateIfRequiredOrScript();
            for (var i = 0; i < changes.Count; i++)
            {
                if (serializedObject.FindProperty(changes[i].PropertyPath) == null)
                {
                    errorMessage = $"SerializedProperty path was not found: {changes[i].PropertyPath}.";
                    return false;
                }
            }

            errorMessage = string.Empty;
            return true;
        }

        internal readonly struct State
        {
            /// <summary> Initializes a new instance of the <see cref="State" /> struct. </summary>
            /// <param name="component"> The resolved scene component whose serialized properties are addressed by <paramref name="changes" />. </param>
            /// <param name="resource"> The scene resource that owns <paramref name="component" />. </param>
            /// <param name="targetAssetPath"> The Prefab asset path that apply/revert is constrained to. </param>
            /// <param name="changes"> The effective request-attributed changes selected for the operation. </param>
            /// <param name="requiresExplicitPrefabAssetMutation"> Whether apply/revert must mutate the explicit Prefab asset because Unity Prefab instance linkage is unavailable for this request-attributed change. </param>
            /// <param name="explicitPrefabAssetHierarchyPath"> The hierarchy path inside <paramref name="targetAssetPath" /> that corresponds to <paramref name="component" /> when <paramref name="requiresExplicitPrefabAssetMutation" /> is <see langword="true" />. </param>
            public State (
                Component component,
                OperationResource resource,
                string targetAssetPath,
                IReadOnlyList<OperationExecutionContext.PrefabOverridePropertyChange> changes,
                bool requiresExplicitPrefabAssetMutation,
                string? explicitPrefabAssetHierarchyPath)
            {
                Component = component;
                Resource = resource;
                TargetAssetPath = targetAssetPath;
                Changes = changes;
                RequiresExplicitPrefabAssetMutation = requiresExplicitPrefabAssetMutation;
                ExplicitPrefabAssetHierarchyPath = explicitPrefabAssetHierarchyPath;
            }

            /// <summary> Gets the resolved scene component whose serialized properties are addressed by <see cref="Changes" />. </summary>
            public Component Component { get; }

            /// <summary> Gets the scene resource that owns <see cref="Component" />. </summary>
            public OperationResource Resource { get; }

            /// <summary> Gets the Prefab asset path that apply/revert is constrained to. </summary>
            public string TargetAssetPath { get; }

            /// <summary> Gets the effective request-attributed changes selected for the operation. </summary>
            public IReadOnlyList<OperationExecutionContext.PrefabOverridePropertyChange> Changes { get; }

            /// <summary> Gets a value indicating whether apply/revert must load and mutate <see cref="TargetAssetPath" /> explicitly. </summary>
            public bool RequiresExplicitPrefabAssetMutation { get; }

            /// <summary> Gets the Prefab asset hierarchy path selected for explicit Prefab asset mutation. </summary>
            public string? ExplicitPrefabAssetHierarchyPath { get; }
        }
    }
}
