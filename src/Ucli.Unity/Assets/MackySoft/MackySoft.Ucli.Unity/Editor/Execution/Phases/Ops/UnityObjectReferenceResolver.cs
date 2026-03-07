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
            if (executionContext == null)
            {
                throw new ArgumentNullException(nameof(executionContext));
            }

            switch (reference.Kind)
            {
                case UnityObjectReferenceKind.Alias:
                    return TryResolveAlias(reference.Alias!, executionContext, out unityObject, out errorMessage);

                case UnityObjectReferenceKind.Selector:
                    if (!ResolveReferenceResolver.TryResolve(reference.Selector, out var resolvedReference, out errorMessage))
                    {
                        unityObject = null;
                        return false;
                    }

                    return TryResolveResolvedReference(resolvedReference!, out unityObject, out errorMessage);

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
            gameObject = null;
            if (!TryResolve(reference, executionContext, out var unityObject, out errorMessage))
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
            if (unityObject == null)
            {
                throw new ArgumentNullException(nameof(unityObject));
            }

            var globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(unityObject);
            return new ResolvedReference(globalObjectId.ToString());
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