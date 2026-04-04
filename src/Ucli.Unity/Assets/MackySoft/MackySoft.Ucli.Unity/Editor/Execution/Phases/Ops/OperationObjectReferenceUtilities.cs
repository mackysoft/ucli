using System;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Provides shared Unity-object reference helpers for operation implementations. </summary>
    internal static class OperationObjectReferenceUtilities
    {
        internal enum ReferenceResolutionPolicy
        {
            LiveOnly = 0,
            AllowTemporaryAliases = 1,
            AllowTemporaryState = 2,
        }

        /// <summary> Resolves one reference to a Unity object according to the specified request-local resolution policy. </summary>
        /// <param name="reference"> The parsed Unity-object reference. </param>
        /// <param name="executionContext"> The request execution context. </param>
        /// <param name="resolutionPolicy"> The temporary-state participation policy. </param>
        /// <param name="unityObject"> The resolved object when successful. </param>
        /// <param name="errorMessage"> The validation error message when resolution fails. </param>
        /// <returns> <see langword="true" /> when the reference resolves; otherwise <see langword="false" />. </returns>
        public static bool TryResolveUnityObject (
            UnityObjectReference reference,
            OperationExecutionContext executionContext,
            ReferenceResolutionPolicy resolutionPolicy,
            out UnityEngine.Object? unityObject,
            out string errorMessage)
        {
            if (executionContext == null)
            {
                throw new ArgumentNullException(nameof(executionContext));
            }

            unityObject = null;
            if (resolutionPolicy >= ReferenceResolutionPolicy.AllowTemporaryAliases
                && reference.Kind == UnityObjectReferenceKind.Alias
                && executionContext.TryGetTemporaryAliasState(reference.Alias!, out var temporaryAliasState))
            {
                unityObject = temporaryAliasState.UnityObject;
                errorMessage = string.Empty;
                return true;
            }

            if (resolutionPolicy == ReferenceResolutionPolicy.AllowTemporaryState)
            {
                return UnityObjectReferenceResolver.TryResolve(
                    reference,
                    executionContext,
                    allowTemporaryState: true,
                    out unityObject,
                    out errorMessage);
            }

            return UnityObjectReferenceResolver.TryResolve(reference, executionContext, out unityObject, out errorMessage);
        }
    }
}
