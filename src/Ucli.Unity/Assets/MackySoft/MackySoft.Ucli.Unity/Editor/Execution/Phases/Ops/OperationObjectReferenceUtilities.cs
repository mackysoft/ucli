using System;
using MackySoft.Ucli.Unity.Execution.Requests;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Provides shared Unity-object reference helpers for operation implementations. </summary>
    internal static class OperationObjectReferenceUtilities
    {
        /// <summary> Defines how request-local aliases, mirrors, and shadows participate in Unity-object reference resolution. </summary>
        internal enum ReferenceResolutionPolicy
        {
            /// <summary> Resolves only against current live editor state. </summary>
            LiveOnly = 0,

            /// <summary> Resolves live state and temporary aliases, but does not allow request-local selector overrides. </summary>
            AllowTemporaryAliases = 1,

            /// <summary> Resolves live state, temporary aliases, and request-local mirrors, shadows, and deletions. </summary>
            AllowTemporaryState = 2,
        }

        /// <summary> Selects the reference-resolution policy required by one operation phase. </summary>
        /// <param name="operation"> The normalized operation whose alias eligibility must be honored. </param>
        /// <param name="allowTemporaryState"> Whether plan-time mirrors, shadows, and deletions participate in resolution. </param>
        /// <returns> The single resolution policy matching the phase and operation contract. </returns>
        /// <exception cref="ArgumentNullException"> <paramref name="operation" /> is <see langword="null" />. </exception>
        public static ReferenceResolutionPolicy GetReferenceResolutionPolicy (
            NormalizedOperation operation,
            bool allowTemporaryState)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            if (allowTemporaryState)
            {
                return ReferenceResolutionPolicy.AllowTemporaryState;
            }

            return operation.AllowRequestLocalAliases
                ? ReferenceResolutionPolicy.AllowTemporaryAliases
                : ReferenceResolutionPolicy.LiveOnly;
        }

        /// <summary> Resolves one reference to a Unity object according to the specified request-local resolution policy. </summary>
        /// <param name="reference"> The parsed Unity-object reference. </param>
        /// <param name="executionContext"> The request execution context. </param>
        /// <param name="resolutionPolicy"> The temporary-state participation policy. </param>
        /// <param name="resolution"> The selected object and any temporary-alias resource provenance when successful. </param>
        /// <param name="errorMessage"> The validation error message when resolution fails. </param>
        /// <returns> <see langword="true" /> when the reference resolves; otherwise <see langword="false" />. </returns>
        public static bool TryResolveUnityObject (
            UnityObjectReference reference,
            OperationExecutionContext executionContext,
            ReferenceResolutionPolicy resolutionPolicy,
            out UnityObjectResolutionState resolution,
            out string errorMessage)
        {
            if (executionContext == null)
            {
                throw new ArgumentNullException(nameof(executionContext));
            }

            resolution = default;
            var hasSelectedTemporaryAlias = TryGetSelectedTemporaryAliasState(
                    reference,
                    executionContext,
                    resolutionPolicy,
                    out var selectedTemporaryAliasState,
                    out var aliasSelectionErrorMessage);
            if (!string.IsNullOrEmpty(aliasSelectionErrorMessage))
            {
                errorMessage = aliasSelectionErrorMessage;
                return false;
            }

            if (hasSelectedTemporaryAlias)
            {
                resolution = new UnityObjectResolutionState(
                    selectedTemporaryAliasState.UnityObject,
                    selectedTemporaryAliasState.Resource,
                    selectedTemporaryAliasState.SourceTrackingKey);
                errorMessage = string.Empty;
                return true;
            }

            switch (resolutionPolicy)
            {
                case ReferenceResolutionPolicy.LiveOnly:
                case ReferenceResolutionPolicy.AllowTemporaryAliases:
                    if (!UnityObjectReferenceResolver.TryResolve(
                            reference,
                            executionContext,
                            allowTemporaryState: false,
                            out var liveUnityObject,
                            out errorMessage))
                    {
                        return false;
                    }

                    resolution = new UnityObjectResolutionState(liveUnityObject);
                    return true;

                case ReferenceResolutionPolicy.AllowTemporaryState:
                    if (!UnityObjectReferenceResolver.TryResolve(
                            reference,
                            executionContext,
                            allowTemporaryState: true,
                            out var temporaryStateUnityObject,
                            out errorMessage))
                    {
                        return false;
                    }

                    resolution = new UnityObjectResolutionState(temporaryStateUnityObject);
                    return true;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(resolutionPolicy),
                        resolutionPolicy,
                        "Unsupported Unity-object reference resolution policy.");
            }
        }

        /// <summary> Gets the temporary alias state selected by one reference resolution policy. </summary>
        /// <param name="reference"> The parsed Unity-object reference. </param>
        /// <param name="executionContext"> The request execution context. </param>
        /// <param name="resolutionPolicy"> The temporary-state participation policy. </param>
        /// <param name="temporaryAliasState"> The selected temporary alias state when temporary state supplies the reference. </param>
        /// <param name="errorMessage"> The alias-binding consistency error, or an empty string when the binding is usable. </param>
        /// <returns> <see langword="true" /> when the policy selects a temporary alias; otherwise <see langword="false" />. </returns>
        private static bool TryGetSelectedTemporaryAliasState (
            UnityObjectReference reference,
            OperationExecutionContext executionContext,
            ReferenceResolutionPolicy resolutionPolicy,
            out TemporaryAliasRegistry.TemporaryAliasState temporaryAliasState,
            out string errorMessage)
        {
            if (executionContext == null)
            {
                throw new ArgumentNullException(nameof(executionContext));
            }

            temporaryAliasState = default;
            errorMessage = string.Empty;
            if (reference.Kind != UnityObjectReferenceKind.Alias)
            {
                return false;
            }

            switch (resolutionPolicy)
            {
                case ReferenceResolutionPolicy.LiveOnly:
                    return false;

                case ReferenceResolutionPolicy.AllowTemporaryAliases:
                    return !executionContext.AliasStore.TryGet(reference.Alias!, out _)
                        && executionContext.TryGetTemporaryAliasState(reference.Alias!, out temporaryAliasState);

                case ReferenceResolutionPolicy.AllowTemporaryState:
                    if (!executionContext.TryGetTemporaryAliasState(reference.Alias!, out temporaryAliasState))
                    {
                        return false;
                    }

                    if (temporaryAliasState.SourceTrackingKey != null
                        && executionContext.AliasStore.TryGet(reference.Alias!, out var stableGlobalObjectId)
                        && !temporaryAliasState.SourceTrackingKey.Equals(
                            RequestLocalObjectIdentity.FromGlobalObjectId(stableGlobalObjectId)))
                    {
                        errorMessage = $"Reference alias has inconsistent stable and request-local source identities: {reference.Alias}.";
                        temporaryAliasState = default;
                        return false;
                    }

                    return true;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(resolutionPolicy),
                        resolutionPolicy,
                        "Unsupported Unity-object reference resolution policy.");
            }
        }

        /// <summary> Carries one resolved Unity object together with the resource provenance selected from a temporary alias. </summary>
        internal readonly struct UnityObjectResolutionState
        {
            public UnityObjectResolutionState (
                UnityEngine.Object unityObject,
                OperationResource? temporaryAliasResource = null,
                RequestLocalObjectIdentity? temporaryAliasSourceTrackingKey = null)
            {
                if (unityObject == null)
                {
                    throw new ArgumentNullException(nameof(unityObject));
                }

                UnityObject = unityObject;
                TemporaryAliasResource = temporaryAliasResource;
                TemporaryAliasSourceTrackingKey = temporaryAliasSourceTrackingKey;
            }

            /// <summary> Gets the resolved live or request-local Unity object. </summary>
            public UnityEngine.Object UnityObject { get; }

            /// <summary> Gets the resource selected with a temporary alias, or <see langword="null" /> when another source supplied the object. </summary>
            public OperationResource? TemporaryAliasResource { get; }

            /// <summary> Gets the semantic source identity carried by the selected temporary alias, or <see langword="null" /> when unavailable. </summary>
            public RequestLocalObjectIdentity? TemporaryAliasSourceTrackingKey { get; }
        }
    }
}
