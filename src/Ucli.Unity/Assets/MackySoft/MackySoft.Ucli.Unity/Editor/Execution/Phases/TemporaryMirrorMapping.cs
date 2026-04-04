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

        private readonly Dictionary<UnityEngine.Object, UnityEngine.Object> stableSourceObjectsBySource =
            new Dictionary<UnityEngine.Object, UnityEngine.Object>(ReferenceEqualityComparer.Instance);

        private readonly List<ComponentPair> componentPairs = new List<ComponentPair>();

        public IReadOnlyList<ComponentPair> ComponentPairs => componentPairs;

        public void AddObjectPair (
            UnityEngine.Object sourceObject,
            UnityEngine.Object previewObject)
        {
            previewObjectsBySource[sourceObject] = previewObject;
            sourceObjectsByPreview[previewObject] = sourceObject;
        }

        public void AddComponentPair (
            Component sourceComponent,
            Component previewComponent)
        {
            AddObjectPair(sourceComponent, previewComponent);
            componentPairs.Add(new ComponentPair(sourceComponent, previewComponent));
        }

        public bool TryGetPreviewObject (
            UnityEngine.Object sourceObject,
            out UnityEngine.Object? previewObject)
        {
            return previewObjectsBySource.TryGetValue(sourceObject, out previewObject);
        }

        public bool TryGetSourceObject (
            UnityEngine.Object previewObject,
            out UnityEngine.Object? sourceObject)
        {
            return sourceObjectsByPreview.TryGetValue(previewObject, out sourceObject);
        }

        public void AddStableSourcePair (
            UnityEngine.Object sourceObject,
            UnityEngine.Object stableSourceObject)
        {
            stableSourceObjectsBySource[sourceObject] = stableSourceObject;
        }

        public bool TryGetStableSourceObject (
            UnityEngine.Object sourceObject,
            out UnityEngine.Object? stableSourceObject)
        {
            return stableSourceObjectsBySource.TryGetValue(sourceObject, out stableSourceObject);
        }

        public readonly struct ComponentPair
        {
            public ComponentPair (
                Component sourceComponent,
                Component previewComponent)
            {
                SourceComponent = sourceComponent;
                PreviewComponent = previewComponent;
            }

            public Component SourceComponent { get; }

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
