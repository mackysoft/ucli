using System;
using UnityEditor;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Resolves parsed Unity-object references to live Unity objects. </summary>
    internal static class UnityObjectReferenceResolver
    {
        /// <summary> Tries to resolve one Unity-object reference to a live Unity object. </summary>
        /// <param name="reference"> The parsed reference. </param>
        /// <param name="executionContext"> The request execution context. </param>
        /// <param name="unityObject"> The resolved Unity object when successful. </param>
        /// <param name="errorMessage"> The resolution error message when resolution fails. </param>
        /// <returns> <see langword="true" /> when resolution succeeds; otherwise <see langword="false" />. </returns>
        public static bool TryResolve (
            UnityObjectReference reference,
            OperationExecutionContext executionContext,
            out UnityEngine.Object? unityObject,
            out string errorMessage)
        {
            return TryResolve(reference, executionContext, allowTemporaryState: false, out unityObject, out errorMessage);
        }

        /// <summary> Tries to resolve one Unity-object reference to a live Unity object. </summary>
        /// <param name="reference"> The parsed reference. </param>
        /// <param name="executionContext"> The request execution context. Must not be <see langword="null" />. </param>
        /// <param name="allowTemporaryState"> <see langword="true" /> to allow request-local prefab planning state to satisfy selector resolution before persisted state is consulted. </param>
        /// <param name="unityObject"> The resolved Unity object when successful. </param>
        /// <param name="errorMessage"> The resolution error message when resolution fails. </param>
        /// <returns> <see langword="true" /> when resolution succeeds; otherwise <see langword="false" />. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="executionContext" /> is <see langword="null" />. </exception>
        public static bool TryResolve (
            UnityObjectReference reference,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out UnityEngine.Object? unityObject,
            out string errorMessage)
        {
            if (executionContext == null)
            {
                throw new ArgumentNullException(nameof(executionContext));
            }

            switch (reference.Kind)
            {
                case UnityObjectReferenceKind.Alias:
                    return TryResolveAlias(reference.Alias!, executionContext, out unityObject, out errorMessage);

                case UnityObjectReferenceKind.Selector:
                    return ResolveReferenceResolver.TryResolveUnityObject(
                        reference.Selector,
                        executionContext,
                        allowTemporaryState,
                        out unityObject,
                        out errorMessage);

                default:
                    unityObject = null;
                    errorMessage = $"Unsupported Unity-object reference kind '{reference.Kind}'.";
                    return false;
            }
        }

        /// <summary> Tries to resolve one Unity-object reference to a live <see cref="GameObject" />. </summary>
        /// <param name="reference"> The parsed reference. </param>
        /// <param name="executionContext"> The request execution context. </param>
        /// <param name="gameObject"> The resolved GameObject when successful. </param>
        /// <param name="errorMessage"> The resolution error message when resolution fails. </param>
        /// <returns> <see langword="true" /> when resolution succeeds; otherwise <see langword="false" />. </returns>
        public static bool TryResolveGameObject (
            UnityObjectReference reference,
            OperationExecutionContext executionContext,
            out GameObject? gameObject,
            out string errorMessage)
        {
            return TryResolveGameObject(reference, executionContext, allowTemporaryState: false, out gameObject, out errorMessage);
        }

        /// <summary> Tries to resolve one Unity-object reference to a live <see cref="GameObject" />. </summary>
        /// <param name="reference"> The parsed reference. </param>
        /// <param name="executionContext"> The request execution context. Must not be <see langword="null" />. </param>
        /// <param name="allowTemporaryState"> <see langword="true" /> to allow request-local prefab planning state to satisfy selector resolution before persisted state is consulted. </param>
        /// <param name="gameObject"> The resolved GameObject when successful. </param>
        /// <param name="errorMessage"> The resolution error message when resolution fails. </param>
        /// <returns> <see langword="true" /> when resolution succeeds and the resolved object is a <see cref="GameObject" />; otherwise <see langword="false" />. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="executionContext" /> is <see langword="null" />. </exception>
        public static bool TryResolveGameObject (
            UnityObjectReference reference,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out GameObject? gameObject,
            out string errorMessage)
        {
            gameObject = null;
            if (!TryResolve(reference, executionContext, allowTemporaryState, out var unityObject, out errorMessage))
            {
                return false;
            }

            gameObject = unityObject as GameObject;
            if (gameObject == null)
            {
                errorMessage = "Reference did not resolve to a GameObject.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        /// <summary> Creates one normalized resolved reference from a live Unity object. </summary>
        /// <param name="unityObject"> The live Unity object. </param>
        /// <returns> The normalized resolved reference. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityObject" /> is <see langword="null" />. </exception>
        public static ResolvedReference CreateResolvedReference (UnityEngine.Object unityObject)
        {
            if (!TryCreateResolvedReference(unityObject, out var resolvedReference))
            {
                throw new InvalidOperationException("Unity object does not expose a stable GlobalObjectId in the current editor state.");
            }

            return resolvedReference!;
        }

        /// <summary> Tries to create one normalized resolved reference from a live Unity object. </summary>
        /// <param name="unityObject"> The live Unity object. </param>
        /// <param name="resolvedReference"> The normalized resolved reference when successful. </param>
        /// <returns> <see langword="true" /> when the object exposes a stable GlobalObjectId; otherwise <see langword="false" />. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityObject" /> is <see langword="null" />. </exception>
        public static bool TryCreateResolvedReference (
            UnityEngine.Object unityObject,
            out ResolvedReference? resolvedReference)
        {
            if (unityObject == null)
            {
                throw new ArgumentNullException(nameof(unityObject));
            }

            var globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(unityObject).ToString();
            if (!GlobalObjectId.TryParse(globalObjectId, out var parsedGlobalObjectId))
            {
                resolvedReference = null;
                return false;
            }

            resolvedReference = new ResolvedReference(parsedGlobalObjectId.ToString());
            return true;
        }

        /// <summary> Creates one request-local tracking key for a live Unity object. </summary>
        /// <param name="unityObject"> The live Unity object. </param>
        /// <returns> One stable tracking key for the current request. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityObject" /> is <see langword="null" />. </exception>
        public static string CreateTrackingKey (UnityEngine.Object unityObject)
        {
            if (unityObject == null)
            {
                throw new ArgumentNullException(nameof(unityObject));
            }

            if (TryCreateResolvedReference(unityObject, out var resolvedReference))
            {
                return resolvedReference!.GlobalObjectId;
            }

            // NOTE:
            // Objects inside prefab stages or request-local planning sandboxes can be live and editable
            // without exposing a stable GlobalObjectId. Within one request, instance IDs are sufficient
            // to correlate operation-local state such as ensured components and component shadows.
            return $"instance:{unityObject.GetInstanceID()}";
        }

        /// <summary> Tries to resolve one alias to a live Unity object. </summary>
        /// <param name="alias"> The alias name. </param>
        /// <param name="executionContext"> The request execution context. </param>
        /// <param name="unityObject"> The resolved Unity object when successful. </param>
        /// <param name="errorMessage"> The resolution error message when resolution fails. </param>
        /// <returns> <see langword="true" /> when resolution succeeds; otherwise <see langword="false" />. </returns>
        private static bool TryResolveAlias (
            string alias,
            OperationExecutionContext executionContext,
            out UnityEngine.Object? unityObject,
            out string errorMessage)
        {
            unityObject = null;
            if (!executionContext.AliasStore.TryGet(alias, out var resolvedReference) || resolvedReference == null)
            {
                errorMessage = $"Reference alias was not found: {alias}.";
                return false;
            }

            return TryResolveResolvedReference(resolvedReference, out unityObject, out errorMessage);
        }

        /// <summary> Tries to resolve one normalized resolved reference to a live Unity object. </summary>
        /// <param name="resolvedReference"> The normalized resolved reference. </param>
        /// <param name="unityObject"> The resolved Unity object when successful. </param>
        /// <param name="errorMessage"> The resolution error message when resolution fails. </param>
        /// <returns> <see langword="true" /> when resolution succeeds; otherwise <see langword="false" />. </returns>
        private static bool TryResolveResolvedReference (
            ResolvedReference resolvedReference,
            out UnityEngine.Object? unityObject,
            out string errorMessage)
        {
            unityObject = null;
            if (!GlobalObjectId.TryParse(resolvedReference.GlobalObjectId, out var globalObjectId))
            {
                errorMessage = $"Resolved reference is malformed: {resolvedReference.GlobalObjectId}.";
                return false;
            }

            unityObject = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalObjectId);
            if (unityObject == null)
            {
                errorMessage = $"Resolved reference is not available in current project state: {resolvedReference.GlobalObjectId}.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }
    }
}
