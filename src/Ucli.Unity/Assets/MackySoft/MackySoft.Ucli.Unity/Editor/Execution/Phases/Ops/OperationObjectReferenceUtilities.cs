using System;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Provides shared Unity-object reference helpers for operation implementations. </summary>
    internal static class OperationObjectReferenceUtilities
    {
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
            if (executionContext == null)
            {
                throw new ArgumentNullException(nameof(executionContext));
            }

            unityObject = null;
            if (allowTemporaryState
                && reference.Kind == UnityObjectReferenceKind.Alias
                && executionContext.TryGetTemporaryAliasState(reference.Alias!, out var temporaryAliasState))
            {
                unityObject = temporaryAliasState.UnityObject;
                errorMessage = string.Empty;
                return true;
            }

            return UnityObjectReferenceResolver.TryResolve(reference, executionContext, out unityObject, out errorMessage);
        }
    }
}