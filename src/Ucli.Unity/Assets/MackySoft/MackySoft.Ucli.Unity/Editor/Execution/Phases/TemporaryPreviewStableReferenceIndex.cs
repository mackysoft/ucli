using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Tracks explicit stable GlobalObjectId entries for request-local preview objects. </summary>
    internal sealed class TemporaryPreviewStableReferenceIndex
    {
        private readonly Dictionary<UnityEngine.Object, string> stableReferencesByPreviewObject =
            new Dictionary<UnityEngine.Object, string>(ReferenceEqualityComparer.Instance);

        private readonly Dictionary<string, UnityEngine.Object> previewObjectsByStableReference =
            new Dictionary<string, UnityEngine.Object>(StringComparer.Ordinal);

        /// <summary> Registers one stable GlobalObjectId for one preview object. </summary>
        /// <param name="previewObject"> The preview object. </param>
        /// <param name="stableReference"> The stable GlobalObjectId text. </param>
        public void Add (
            UnityEngine.Object previewObject,
            string stableReference)
        {
            if (previewObject == null)
            {
                throw new ArgumentNullException(nameof(previewObject));
            }

            if (string.IsNullOrWhiteSpace(stableReference))
            {
                throw new ArgumentException("Stable reference must not be null, empty, or whitespace.", nameof(stableReference));
            }

            if (stableReferencesByPreviewObject.TryGetValue(previewObject, out var existingStableReference))
            {
                if (string.Equals(existingStableReference, stableReference, StringComparison.Ordinal))
                {
                    return;
                }

                throw new InvalidOperationException(
                    $"Preview object '{previewObject.name}' is already bound to stable reference '{existingStableReference}' and cannot be rebound to '{stableReference}'.");
            }

            if (previewObjectsByStableReference.TryGetValue(stableReference, out var existingPreviewObject))
            {
                if (ReferenceEquals(existingPreviewObject, previewObject))
                {
                    return;
                }

                throw new InvalidOperationException(
                    $"Stable reference '{stableReference}' is already bound to preview object '{existingPreviewObject.name}' and cannot be rebound to '{previewObject.name}'.");
            }

            stableReferencesByPreviewObject.Add(previewObject, stableReference);
            previewObjectsByStableReference.Add(stableReference, previewObject);
        }

        /// <summary> Tries to resolve one preview object to its stable GlobalObjectId text. </summary>
        /// <param name="previewObject"> The preview object. </param>
        /// <param name="stableReference"> The stable GlobalObjectId text when found. </param>
        /// <returns> <see langword="true" /> when the preview object has one stable reference entry; otherwise <see langword="false" />. </returns>
        public bool TryGetStableReference (
            UnityEngine.Object previewObject,
            out string stableReference)
        {
            return stableReferencesByPreviewObject.TryGetValue(previewObject, out stableReference!);
        }

        /// <summary> Tries to resolve one stable GlobalObjectId text back to its preview object. </summary>
        /// <param name="stableReference"> The stable GlobalObjectId text. </param>
        /// <param name="previewObject"> The preview object when found. </param>
        /// <returns> <see langword="true" /> when the stable reference maps into the preview index; otherwise <see langword="false" />. </returns>
        public bool TryGetPreviewObject (
            string stableReference,
            out UnityEngine.Object? previewObject)
        {
            return previewObjectsByStableReference.TryGetValue(stableReference, out previewObject);
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
