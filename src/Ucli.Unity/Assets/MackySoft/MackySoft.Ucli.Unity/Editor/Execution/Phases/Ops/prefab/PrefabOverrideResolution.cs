using System.Collections.Generic;
using System.Linq;
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
            if (!PrefabOperationUtilities.TryEnsurePrefabAssetExists(targetAssetPath, out errorMessage))
            {
                return false;
            }

            if (!PrefabUtility.IsPartOfPrefabInstance(component))
            {
                errorMessage = "Prefab override actions require a Prefab instance component target.";
                return false;
            }

            if (PrefabUtility.GetCorrespondingObjectFromSourceAtPath(component, targetAssetPath) == null)
            {
                errorMessage = $"Prefab override target asset is not in the target instance lineage: {targetAssetPath}.";
                return false;
            }

            var targetKey = CreateTargetKey(targetReference, component);
            var requestedPropertyPaths = args.PropertyPaths?.Select(static path => path.Value).ToArray();
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

            if (!TryValidateProperties(component, changes, out errorMessage))
            {
                return false;
            }

            state = new State(component, componentResolution.Resource, targetAssetPath, changes);
            return true;
        }

        private static bool TryResolveComponentTarget (
            UnityObjectReference targetReference,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out ComponentOperationUtilities.ComponentResolutionState componentResolution,
            out string errorMessage)
        {
            if (!ComponentOperationUtilities.TryResolveComponent(
                    targetReference,
                    executionContext,
                    allowTemporaryState,
                    out componentResolution,
                    out errorMessage))
            {
                return false;
            }

            if (allowTemporaryState
                && componentResolution.Component != null
                && !PrefabUtility.IsPartOfPrefabInstance(componentResolution.Component)
                && ComponentOperationUtilities.TryResolveComponent(
                    targetReference,
                    executionContext,
                    allowTemporaryState: false,
                    out var liveComponentResolution,
                    out _))
            {
                componentResolution = liveComponentResolution;
            }

            errorMessage = string.Empty;
            return true;
        }

        private static string CreateTargetKey (
            UnityObjectReference targetReference,
            Component component)
        {
            if (targetReference.Kind == UnityObjectReferenceKind.Selector
                && targetReference.Selector.Kind == ResolveSelectorKind.GlobalObjectId)
            {
                return targetReference.Selector.GlobalObjectId!;
            }

            return UnityObjectReferenceResolver.CreateTrackingKey(component);
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
            public State (
                Component component,
                OperationResource resource,
                string targetAssetPath,
                IReadOnlyList<OperationExecutionContext.PrefabOverridePropertyChange> changes)
            {
                Component = component;
                Resource = resource;
                TargetAssetPath = targetAssetPath;
                Changes = changes;
            }

            public Component Component { get; }

            public OperationResource Resource { get; }

            public string TargetAssetPath { get; }

            public IReadOnlyList<OperationExecutionContext.PrefabOverridePropertyChange> Changes { get; }
        }
    }
}
