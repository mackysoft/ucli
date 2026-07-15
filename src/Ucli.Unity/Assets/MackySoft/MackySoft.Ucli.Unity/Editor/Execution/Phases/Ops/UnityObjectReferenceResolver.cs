using System;
using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;
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
            [NotNullWhen(true)] out UnityEngine.Object? unityObject,
            out string errorMessage)
        {
            if (reference == null)
            {
                throw new ArgumentNullException(nameof(reference));
            }

            if (executionContext == null)
            {
                throw new ArgumentNullException(nameof(executionContext));
            }

            switch (reference.Kind)
            {
                case UnityObjectReferenceKind.Alias:
                    return TryResolveAlias(
                        reference.Alias!,
                        executionContext,
                        allowTemporaryState,
                        out unityObject,
                        out errorMessage);

                case UnityObjectReferenceKind.Selector:
                    return ResolveReferenceResolver.TryResolveUnityObject(
                        reference.Selector!,
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
            [NotNullWhen(true)] out GameObject? gameObject,
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

        /// <summary> Creates one canonical GlobalObjectId from a live Unity object. </summary>
        /// <param name="unityObject"> The live Unity object. </param>
        /// <returns> The canonical object identity. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityObject" /> is <see langword="null" />. </exception>
        public static UnityGlobalObjectId CreateGlobalObjectId (UnityEngine.Object unityObject)
        {
            if (!TryCreateStableGlobalObjectId(unityObject, out var globalObjectId))
            {
                throw new InvalidOperationException("Unity object does not expose a stable GlobalObjectId in the current editor state.");
            }

            return globalObjectId;
        }

        /// <summary> Tries to get one parsed stable GlobalObjectId from a live Unity object. </summary>
        /// <param name="unityObject"> The live Unity object. </param>
        /// <param name="globalObjectId"> The parsed stable GlobalObjectId when available; otherwise the default value. </param>
        /// <returns> <see langword="true" /> when the object exposes a stable GlobalObjectId; otherwise <see langword="false" />. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityObject" /> is destroyed or <see langword="null" />. </exception>
        public static bool TryCreateStableGlobalObjectId (
            UnityEngine.Object unityObject,
            [NotNullWhen(true)] out UnityGlobalObjectId? globalObjectId)
        {
            if (unityObject == null)
            {
                throw new ArgumentNullException(nameof(unityObject));
            }

            var candidate = GlobalObjectId.GetGlobalObjectIdSlow(unityObject).ToString();
            if (!UnityGlobalObjectId.TryParse(candidate, out globalObjectId))
            {
                return false;
            }

            return true;
        }

        /// <summary> Tries to resolve one alias to a live Unity object. </summary>
        /// <param name="alias"> The alias name. </param>
        /// <param name="executionContext"> The request execution context. </param>
        /// <param name="allowTemporaryState"> Whether request-local preview, shadow, and deletion state participates in resolution. </param>
        /// <param name="unityObject"> The resolved Unity object when successful. </param>
        /// <param name="errorMessage"> The resolution error message when resolution fails. </param>
        /// <returns> <see langword="true" /> when resolution succeeds; otherwise <see langword="false" />. </returns>
        private static bool TryResolveAlias (
            RequestLocalAliasIdentity alias,
            OperationExecutionContext executionContext,
            bool allowTemporaryState,
            out UnityEngine.Object? unityObject,
            out string errorMessage)
        {
            unityObject = null;
            if (!executionContext.AliasStore.TryGet(alias, out var globalObjectId))
            {
                errorMessage = $"Reference alias was not found: {alias.Alias}.";
                return false;
            }

            return ResolveReferenceResolver.TryResolveUnityObject(
                ResolveSelector.FromGlobalObjectId(globalObjectId),
                executionContext,
                allowTemporaryState,
                out unityObject,
                out errorMessage);
        }

    }
}
