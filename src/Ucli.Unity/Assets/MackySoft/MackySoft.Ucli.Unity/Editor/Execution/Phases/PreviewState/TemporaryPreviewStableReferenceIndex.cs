using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using MackySoft.Ucli.Contracts.Ipc;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Tracks explicit stable GlobalObjectId entries for request-local preview objects. </summary>
    internal sealed class TemporaryPreviewStableReferenceIndex
    {
        private readonly Dictionary<UnityEngine.Object, UnityGlobalObjectId> globalObjectIdsByPreviewObject =
            new Dictionary<UnityEngine.Object, UnityGlobalObjectId>(ReferenceEqualityComparer.Instance);

        private readonly Dictionary<UnityGlobalObjectId, UnityEngine.Object> previewObjectsByGlobalObjectId =
            new Dictionary<UnityGlobalObjectId, UnityEngine.Object>();

        /// <summary> Registers one stable GlobalObjectId for one preview object. </summary>
        /// <param name="previewObject"> The preview object. </param>
        /// <param name="globalObjectId"> The stable source identity. </param>
        public void Add (
            UnityEngine.Object previewObject,
            UnityGlobalObjectId globalObjectId)
        {
            if (previewObject == null)
            {
                throw new ArgumentNullException(nameof(previewObject));
            }

            if (globalObjectId == null)
            {
                throw new ArgumentNullException(nameof(globalObjectId));
            }

            if (globalObjectIdsByPreviewObject.TryGetValue(previewObject, out var existingGlobalObjectId))
            {
                if (existingGlobalObjectId.Equals(globalObjectId))
                {
                    return;
                }

                throw new InvalidOperationException(
                    $"Preview object '{previewObject.name}' is already bound to a different stable reference.");
            }

            if (previewObjectsByGlobalObjectId.TryGetValue(globalObjectId, out var existingPreviewObject))
            {
                if (ReferenceEquals(existingPreviewObject, previewObject))
                {
                    return;
                }

                throw new InvalidOperationException(
                    $"Stable reference is already bound to preview object '{existingPreviewObject.name}' and cannot be rebound to '{previewObject.name}'.");
            }

            globalObjectIdsByPreviewObject.Add(previewObject, globalObjectId);
            previewObjectsByGlobalObjectId.Add(globalObjectId, previewObject);
        }

        /// <summary> Tries to resolve one preview object to its stable source identity. </summary>
        /// <param name="previewObject"> The preview object. </param>
        /// <param name="globalObjectId"> The stable source identity when found. </param>
        /// <returns> <see langword="true" /> when the preview object has one stable reference entry; otherwise <see langword="false" />. </returns>
        public bool TryGetGlobalObjectId (
            UnityEngine.Object previewObject,
            [NotNullWhen(true)] out UnityGlobalObjectId? globalObjectId)
        {
            return globalObjectIdsByPreviewObject.TryGetValue(previewObject, out globalObjectId);
        }

        /// <summary> Tries to resolve one stable source identity back to its preview object. </summary>
        /// <param name="globalObjectId"> The stable source identity. </param>
        /// <param name="previewObject"> The preview object when found. </param>
        /// <returns> <see langword="true" /> when the stable reference maps into the preview index; otherwise <see langword="false" />. </returns>
        public bool TryGetPreviewObject (
            UnityGlobalObjectId globalObjectId,
            out UnityEngine.Object? previewObject)
        {
            if (globalObjectId == null)
            {
                previewObject = null;
                return false;
            }

            return previewObjectsByGlobalObjectId.TryGetValue(globalObjectId, out previewObject);
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<UnityEngine.Object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

            public bool Equals (
                UnityEngine.Object? x,
                UnityEngine.Object? y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode (UnityEngine.Object obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
