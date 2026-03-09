using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Provides reusable helpers shared by component-domain operations. </summary>
    internal static class ComponentOperationUtilities
    {
        /// <summary> Resolves one reference to a loaded-scene GameObject. </summary>
        /// <param name="reference"> The parsed Unity-object reference. </param>
        /// <param name="executionContext"> The request execution context. </param>
        /// <param name="gameObject"> The resolved GameObject when successful. </param>
        /// <param name="scene"> The owning loaded scene when successful. </param>
        /// <param name="errorMessage"> The validation error message when resolution fails. </param>
        /// <returns> <see langword="true" /> when the reference resolves to one loaded-scene GameObject; otherwise <see langword="false" />. </returns>
        public static bool TryResolveLoadedSceneGameObject (
            UnityObjectReference reference,
            OperationExecutionContext executionContext,
            out GameObject? gameObject,
            out Scene scene,
            out string errorMessage)
        {
            return GoOperationUtilities.TryResolveLoadedSceneGameObject(
                reference,
                executionContext,
                out gameObject,
                out scene,
                out errorMessage);
        }

        /// <summary> Resolves one reference to a Component. Temporary plan aliases can be enabled when required. </summary>
        /// <param name="reference"> The parsed Unity-object reference. </param>
        /// <param name="executionContext"> The request execution context. </param>
        /// <param name="allowTemporaryState"> Whether temporary plan aliases may satisfy the reference. </param>
        /// <param name="component"> The resolved component when successful. </param>
        /// <param name="scenePath"> The owning scene path when successful. </param>
        /// <param name="errorMessage"> The validation error message when resolution fails. </param>
        /// <returns> <see langword="true" /> when the reference resolves to one Component target; otherwise <see langword="false" />. </returns>
        public static bool TryResolveComponent (
            UnityObjectReference reference,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out Component? component,
            out string scenePath,
            out string errorMessage)
        {
            component = null;
            scenePath = string.Empty;
            if (allowTemporaryState
                && reference.Kind == UnityObjectReferenceKind.Alias
                && executionContext.TryGetTemporaryAlias(reference.Alias!, out var temporaryObject, out scenePath))
            {
                component = temporaryObject as Component;
                if (component == null)
                {
                    errorMessage = "Reference did not resolve to a Component.";
                    return false;
                }

                errorMessage = string.Empty;
                return true;
            }

            if (!UnityObjectReferenceResolver.TryResolve(reference, executionContext, out var unityObject, out errorMessage))
            {
                return false;
            }

            component = unityObject as Component;
            if (component == null)
            {
                errorMessage = "Reference did not resolve to a Component.";
                return false;
            }

            return TryGetLoadedScenePath(component, out scenePath, out errorMessage);
        }

        /// <summary> Resolves one reference to a Unity object. Temporary plan aliases can be enabled when required. </summary>
        /// <param name="reference"> The parsed Unity-object reference. </param>
        /// <param name="executionContext"> The request execution context. </param>
        /// <param name="allowTemporaryState"> Whether temporary plan aliases may satisfy the reference. </param>
        /// <param name="unityObject"> The resolved object when successful. </param>
        /// <param name="errorMessage"> The validation error message when resolution fails. </param>
        /// <returns> <see langword="true" /> when the reference resolves; otherwise <see langword="false" />. </returns>
        public static bool TryResolveUnityObject (
            UnityObjectReference reference,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out UnityEngine.Object? unityObject,
            out string errorMessage)
        {
            unityObject = null;
            if (allowTemporaryState
                && reference.Kind == UnityObjectReferenceKind.Alias
                && executionContext.TryGetTemporaryAlias(reference.Alias!, out var temporaryObject, out _))
            {
                unityObject = temporaryObject;
                errorMessage = string.Empty;
                return true;
            }

            return UnityObjectReferenceResolver.TryResolve(reference, executionContext, out unityObject, out errorMessage);
        }

        /// <summary> Tries to resolve one loaded-scene path from a Component. </summary>
        /// <param name="component"> The source component. </param>
        /// <param name="scenePath"> The owning scene path when successful. </param>
        /// <param name="errorMessage"> The validation error message when resolution fails. </param>
        /// <returns> <see langword="true" /> when the component belongs to a loaded scene; otherwise <see langword="false" />. </returns>
        public static bool TryGetLoadedScenePath (
            Component component,
            out string scenePath,
            out string errorMessage)
        {
            scenePath = string.Empty;
            if (component == null)
            {
                throw new ArgumentNullException(nameof(component));
            }

            var gameObject = component.gameObject;
            if (!GoOperationUtilities.TryGetLoadedSceneFromGameObject(gameObject, out var scene, out errorMessage))
            {
                return false;
            }

            scenePath = scene.path;
            errorMessage = string.Empty;
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
    }
}