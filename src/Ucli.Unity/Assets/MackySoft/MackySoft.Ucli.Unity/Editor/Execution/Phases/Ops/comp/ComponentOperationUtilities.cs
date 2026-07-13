using System;
using UnityEditor;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Provides reusable helpers shared by component-domain operations. </summary>
    internal static class ComponentOperationUtilities
    {
        /// <summary> Resolves one component selector against the specified GameObject and optional request-local ensure state. </summary>
        /// <param name="gameObject"> The candidate GameObject. </param>
        /// <param name="componentType"> The component type selector. </param>
        /// <param name="executionContext"> The request execution context. </param>
        /// <param name="allowTemporaryState"> Whether request-local ensure state may contribute to the selector result. </param>
        /// <param name="resolution"> The selector resolution result when type parsing succeeds. </param>
        /// <param name="errorMessage"> The validation error message when the component type cannot be parsed. </param>
        /// <returns> <see langword="true" /> when selector evaluation completes; otherwise <see langword="false" />. </returns>
        public static bool TryResolveComponentSelector (
            GameObject gameObject,
            string componentType,
            OperationExecutionContext? executionContext,
            bool allowTemporaryState,
            out ComponentSelectorResolutionState resolution,
            out string errorMessage)
        {
            if (gameObject == null)
            {
                throw new ArgumentNullException(nameof(gameObject));
            }

            resolution = default;
            if (!ComponentTypeResolver.TryResolveComponentType(componentType, out var componentRuntimeType, out errorMessage))
            {
                return false;
            }

            var components = gameObject.GetComponents(componentRuntimeType!);
            Component? ensuredComponent = null;
            var ensuredComponentCount = 0;
            if (allowTemporaryState
                && executionContext != null)
            {
                if (!OperationResourceUtilities.TryResolveOwnerResource(
                        gameObject,
                        executionContext,
                        out var resource,
                        out errorMessage))
                {
                    return false;
                }

                var targetTrackingKey = executionContext.CreateGameObjectTrackingKey(gameObject, resource);
                if (executionContext.TryGetEnsuredComponentState(targetTrackingKey, componentRuntimeType!, out var ensuredComponentState))
                {
                    ensuredComponent = ensuredComponentState.Component;
                    if (ensuredComponent != null)
                    {
                        ensuredComponentCount = 1;
                    }
                }
            }

            var totalComponentCount = components.Length + ensuredComponentCount;
            var resolvedComponent = components.Length == 1
                ? components[0]
                : ensuredComponent;
            resolution = new ComponentSelectorResolutionState(totalComponentCount, resolvedComponent);
            errorMessage = string.Empty;
            return true;
        }

        /// <summary> Resolves one reference to a Component under the specified request-local state policy. </summary>
        /// <param name="reference"> The parsed Unity-object reference. </param>
        /// <param name="executionContext"> The request execution context. </param>
        /// <param name="resolutionPolicy"> The request-local state participation policy. </param>
        /// <param name="resolution"> The resolved component state when successful. </param>
        /// <param name="errorMessage"> The validation error message when resolution fails. </param>
        /// <returns> <see langword="true" /> when the reference resolves to one Component target; otherwise <see langword="false" />. </returns>
        public static bool TryResolveComponent (
            UnityObjectReference reference,
            OperationExecutionContext executionContext,
            OperationObjectReferenceUtilities.ReferenceResolutionPolicy resolutionPolicy,
            out ComponentResolutionState resolution,
            out string errorMessage)
        {
            resolution = default;
            if (!OperationObjectReferenceUtilities.TryResolveUnityObject(
                    reference,
                    executionContext,
                    resolutionPolicy,
                    out var unityObject,
                    out errorMessage))
            {
                return false;
            }

            var component = unityObject as Component;
            if (component == null)
            {
                errorMessage = "Reference did not resolve to a Component.";
                return false;
            }

            if (reference.Kind == UnityObjectReferenceKind.Alias
                && executionContext.TryGetTemporaryAliasState(reference.Alias!, out var temporaryAliasState)
                && ReferenceEquals(temporaryAliasState.UnityObject, component))
            {
                resolution = new ComponentResolutionState(component, temporaryAliasState.Resource);
                errorMessage = string.Empty;
                return true;
            }

            if (resolutionPolicy != OperationObjectReferenceUtilities.ReferenceResolutionPolicy.LiveOnly
                && executionContext.TryResolveTrackedComponentResource(component, out var trackedResource))
            {
                resolution = new ComponentResolutionState(component, trackedResource);
                errorMessage = string.Empty;
                return true;
            }

            if (!OperationResourceUtilities.TryResolveOwnerResource(component, executionContext, out var resource, out errorMessage))
            {
                return false;
            }

            resolution = new ComponentResolutionState(component, resource);
            return true;
        }

        /// <summary> Creates one temporary component instance for plan-time simulation. </summary>
        /// <param name="componentType"> The component runtime type. </param>
        /// <param name="executionContext"> The request execution context. </param>
        /// <param name="component"> The created temporary component when successful. </param>
        /// <param name="errorMessage"> The creation error message when creation fails. </param>
        /// <returns> <see langword="true" /> when temporary component is created; otherwise <see langword="false" />. </returns>
        public static bool TryCreateTemporaryComponent (
            Type componentType,
            OperationExecutionContext executionContext,
            out Component? component,
            out string errorMessage)
        {
            if (componentType == null)
            {
                throw new ArgumentNullException(nameof(componentType));
            }

            var host = new GameObject($"__ucli-comp-temp-{componentType.Name}__")
            {
                // NOTE: HideAndDontSave includes NotEditable, which makes SerializedProperty.editable false.
                hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild,
            };
            executionContext.TrackTemporaryObject(host);
            component = CreateComponentOnHost(host, componentType);
            if (component == null)
            {
                errorMessage = $"Temporary component could not be created for type '{componentType.FullName}'.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        /// <summary> Creates one temporary clone of the specified component for plan-time mutation. </summary>
        /// <param name="source"> The source component. </param>
        /// <param name="executionContext"> The request execution context. </param>
        /// <param name="clone"> The temporary clone when successful. </param>
        /// <param name="errorMessage"> The creation error message when cloning fails. </param>
        /// <returns> <see langword="true" /> when cloning succeeds; otherwise <see langword="false" />. </returns>
        public static bool TryCreateTemporaryComponentClone (
            Component source,
            OperationExecutionContext executionContext,
            out Component? clone,
            out string errorMessage)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (!TryCreateTemporaryComponent(source.GetType(), executionContext, out clone, out errorMessage))
            {
                return false;
            }

            EditorUtility.CopySerialized(source, clone);
            errorMessage = string.Empty;
            return true;
        }

        /// <summary> Copies the serialized state of one component to another component of the same runtime type. </summary>
        /// <param name="source"> The source component. </param>
        /// <param name="target"> The target component. </param>
        public static void CopySerializedState (
            Component source,
            Component target)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            EditorUtility.CopySerialized(source, target);
        }

        private static Component? CreateComponentOnHost (
            GameObject host,
            Type componentType)
        {
            if (componentType == typeof(Transform))
            {
                return host.transform;
            }

            return host.AddComponent(componentType);
        }

        internal readonly struct ComponentResolutionState
        {
            public ComponentResolutionState (
                Component component,
                OperationResource resource)
            {
                Component = component;
                Resource = resource;
            }

            public Component? Component { get; }

            public OperationResource Resource { get; }
        }

        internal readonly struct ComponentSelectorResolutionState
        {
            public ComponentSelectorResolutionState (
                int matchCount,
                Component? component)
            {
                MatchCount = matchCount;
                Component = component;
            }

            public int MatchCount { get; }

            public Component? Component { get; }
        }
    }
}
