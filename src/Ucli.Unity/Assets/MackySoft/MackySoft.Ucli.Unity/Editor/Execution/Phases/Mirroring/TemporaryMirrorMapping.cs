using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Tracks one mirrored live-to-preview object graph so preview objects can be mapped back to their live sources. </summary>
    internal sealed class TemporaryMirrorMapping
    {
        private readonly Dictionary<UnityEngine.Object, UnityEngine.Object> previewObjectsBySource =
            new Dictionary<UnityEngine.Object, UnityEngine.Object>(ReferenceEqualityComparer.Instance);

        private readonly Dictionary<UnityEngine.Object, UnityEngine.Object> sourceObjectsByPreview =
            new Dictionary<UnityEngine.Object, UnityEngine.Object>(ReferenceEqualityComparer.Instance);

        private readonly List<ComponentPair> componentPairs = new List<ComponentPair>();

        /// <summary> Gets the mirrored component pairs registered for the current mirror graph. </summary>
        public IReadOnlyList<ComponentPair> ComponentPairs => componentPairs;

        /// <summary> Registers one live-to-preview object pair in the mirror graph. </summary>
        /// <param name="sourceObject"> The mirrored live source object. </param>
        /// <param name="previewObject"> The request-local preview object. </param>
        public void AddObjectPair (
            UnityEngine.Object sourceObject,
            UnityEngine.Object previewObject)
        {
            if (previewObjectsBySource.TryGetValue(sourceObject, out var existingPreviewObject))
            {
                if (ReferenceEquals(existingPreviewObject, previewObject))
                {
                    return;
                }

                throw new System.InvalidOperationException(
                    $"Source object '{sourceObject.name}' is already bound to preview object '{existingPreviewObject.name}' and cannot be rebound to '{previewObject.name}'.");
            }

            if (sourceObjectsByPreview.TryGetValue(previewObject, out var existingSourceObject))
            {
                if (ReferenceEquals(existingSourceObject, sourceObject))
                {
                    return;
                }

                throw new System.InvalidOperationException(
                    $"Preview object '{previewObject.name}' is already bound to source object '{existingSourceObject.name}' and cannot be rebound to '{sourceObject.name}'.");
            }

            previewObjectsBySource.Add(sourceObject, previewObject);
            sourceObjectsByPreview.Add(previewObject, sourceObject);
        }

        /// <summary> Registers one live-to-preview component pair in the mirror graph. </summary>
        /// <param name="sourceComponent"> The mirrored live source component. </param>
        /// <param name="previewComponent"> The request-local preview component. </param>
        public void AddComponentPair (
            Component sourceComponent,
            Component previewComponent)
        {
            AddObjectPair(sourceComponent, previewComponent);
            componentPairs.Add(new ComponentPair(sourceComponent, previewComponent));
        }

        /// <summary> Tries to resolve one mirrored live source object to its preview counterpart. </summary>
        /// <param name="sourceObject"> The mirrored live source object. </param>
        /// <param name="previewObject"> The request-local preview object when found. </param>
        /// <returns> <see langword="true" /> when the source object is registered in the mirror graph; otherwise <see langword="false" />. </returns>
        public bool TryGetPreviewObject (
            UnityEngine.Object sourceObject,
            out UnityEngine.Object? previewObject)
        {
            return previewObjectsBySource.TryGetValue(sourceObject, out previewObject);
        }

        /// <summary> Tries to resolve one preview object back to its mirrored live source object. </summary>
        /// <param name="previewObject"> The request-local preview object. </param>
        /// <param name="sourceObject"> The mirrored live source object when found. </param>
        /// <returns> <see langword="true" /> when the preview object is registered in the mirror graph; otherwise <see langword="false" />. </returns>
        public bool TryGetSourceObject (
            UnityEngine.Object previewObject,
            out UnityEngine.Object? sourceObject)
        {
            return sourceObjectsByPreview.TryGetValue(previewObject, out sourceObject);
        }

        /// <summary> Represents one mirrored live-to-preview component pair. </summary>
        public readonly struct ComponentPair
        {
            /// <summary> Initializes a new instance of the <see cref="ComponentPair" /> struct. </summary>
            /// <param name="sourceComponent"> The mirrored live source component. </param>
            /// <param name="previewComponent"> The request-local preview component. </param>
            public ComponentPair (
                Component sourceComponent,
                Component previewComponent)
            {
                SourceComponent = sourceComponent;
                PreviewComponent = previewComponent;
            }

            /// <summary> Gets the mirrored live source component. </summary>
            public Component SourceComponent { get; }

            /// <summary> Gets the request-local preview component. </summary>
            public Component PreviewComponent { get; }
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
