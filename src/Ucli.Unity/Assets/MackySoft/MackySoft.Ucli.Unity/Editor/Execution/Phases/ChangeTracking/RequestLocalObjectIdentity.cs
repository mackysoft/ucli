using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using MackySoft.Ucli.Contracts.Ipc;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary>
    /// Identifies one semantic Unity object within a request as either a stable GlobalObjectId or a transient object reference.
    /// </summary>
    internal sealed class RequestLocalObjectIdentity : IEquatable<RequestLocalObjectIdentity>
    {
        private readonly IdentityKind kind;

        private readonly UnityGlobalObjectId? stableGlobalObjectId;

        private readonly UnityEngine.Object? transientUnityObject;

        private RequestLocalObjectIdentity (UnityGlobalObjectId globalObjectId)
        {
            if (globalObjectId == null)
            {
                throw new ArgumentNullException(nameof(globalObjectId));
            }

            kind = IdentityKind.StableGlobalObjectId;
            stableGlobalObjectId = globalObjectId;
            transientUnityObject = null;
        }

        private RequestLocalObjectIdentity (UnityEngine.Object transientUnityObject)
        {
            if (transientUnityObject == null)
            {
                throw new ArgumentNullException(nameof(transientUnityObject));
            }

            kind = IdentityKind.TransientUnityObject;
            stableGlobalObjectId = default;
            this.transientUnityObject = transientUnityObject;
        }

        /// <summary> Creates one request-local identity from a parsed stable GlobalObjectId. </summary>
        /// <param name="globalObjectId"> The parsed GlobalObjectId that identifies a persistent asset or saved scene object. </param>
        /// <returns> A stable request-local identity. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="globalObjectId" /> is <see langword="null" />. </exception>
        public static RequestLocalObjectIdentity FromGlobalObjectId (UnityGlobalObjectId globalObjectId)
        {
            return new RequestLocalObjectIdentity(globalObjectId);
        }

        /// <summary> Creates one request-local identity from a live Unity object, preferring its stable GlobalObjectId when available. </summary>
        /// <param name="unityObject"> The live Unity object whose request-local identity is required. </param>
        /// <returns> A stable or transient identity for <paramref name="unityObject" />. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityObject" /> is destroyed or <see langword="null" />. </exception>
        public static RequestLocalObjectIdentity FromUnityObject (UnityEngine.Object unityObject)
        {
            if (unityObject == null)
            {
                throw new ArgumentNullException(nameof(unityObject));
            }

            return UnityObjectReferenceResolver.TryCreateStableGlobalObjectId(unityObject, out var globalObjectId)
                ? FromGlobalObjectId(globalObjectId)
                : new RequestLocalObjectIdentity(unityObject);
        }

        /// <summary> Tries to get the stable GlobalObjectId carried by this identity. </summary>
        /// <param name="globalObjectId"> The stable GlobalObjectId when this identity is stable; otherwise <see langword="null" />. </param>
        /// <returns> <see langword="true" /> when this identity is stable; otherwise <see langword="false" />. </returns>
        public bool TryGetStableGlobalObjectId ([NotNullWhen(true)] out UnityGlobalObjectId? globalObjectId)
        {
            globalObjectId = stableGlobalObjectId;
            return kind == IdentityKind.StableGlobalObjectId && globalObjectId != null;
        }

        /// <summary> Tries to get the transient Unity object carried by this identity. </summary>
        /// <param name="unityObject"> The live transient Unity object when available; otherwise <see langword="null" />. </param>
        /// <returns> <see langword="true" /> when this identity still references a live transient Unity object; otherwise <see langword="false" />. </returns>
        public bool TryGetTransientUnityObject ([NotNullWhen(true)] out UnityEngine.Object? unityObject)
        {
            unityObject = transientUnityObject;
            return kind == IdentityKind.TransientUnityObject && unityObject != null;
        }

        public bool Equals (RequestLocalObjectIdentity? other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (other == null)
            {
                return false;
            }

            if (kind != other.kind)
            {
                return false;
            }

            switch (kind)
            {
                case IdentityKind.StableGlobalObjectId:
                    return Equals(stableGlobalObjectId, other.stableGlobalObjectId);

                case IdentityKind.TransientUnityObject:
                    return ReferenceEquals(transientUnityObject, other.transientUnityObject);

                default:
                    throw new InvalidOperationException("Unsupported request-local object identity kind.");
            }
        }

        public override bool Equals (object? obj)
        {
            return obj is RequestLocalObjectIdentity other && Equals(other);
        }

        public override int GetHashCode ()
        {
            switch (kind)
            {
                case IdentityKind.StableGlobalObjectId:
                    return stableGlobalObjectId!.GetHashCode();

                case IdentityKind.TransientUnityObject:
                    return RuntimeHelpers.GetHashCode(transientUnityObject!);

                default:
                    throw new InvalidOperationException("Unsupported request-local object identity kind.");
            }
        }

        private enum IdentityKind : byte
        {
            StableGlobalObjectId = 0,
            TransientUnityObject = 1,
        }
    }
}
