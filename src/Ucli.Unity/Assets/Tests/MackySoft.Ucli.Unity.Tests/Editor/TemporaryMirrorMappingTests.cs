using System;
using MackySoft.Ucli.Unity.Execution.Phases;
using NUnit.Framework;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class TemporaryMirrorMappingTests
    {
        [Test]
        [Category("Size.Small")]
        public void AddObjectPair_WhenSamePairIsRegisteredTwice_IsNoOp ()
        {
            var mapping = new TemporaryMirrorMapping();
            var source = new GameObject("Source");
            var preview = new GameObject("Preview");
            try
            {
                mapping.AddObjectPair(source, preview);
                mapping.AddObjectPair(source, preview);

                Assert.That(mapping.TryGetPreviewObject(source, out var resolvedPreview), Is.True);
                Assert.That(resolvedPreview, Is.SameAs(preview));
                Assert.That(mapping.TryGetSourceObject(preview, out var resolvedSource), Is.True);
                Assert.That(resolvedSource, Is.SameAs(source));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(preview);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void AddObjectPair_WhenSourceIsRebound_ThrowsInvalidOperationException ()
        {
            var mapping = new TemporaryMirrorMapping();
            var source = new GameObject("Source");
            var previewA = new GameObject("PreviewA");
            var previewB = new GameObject("PreviewB");
            try
            {
                mapping.AddObjectPair(source, previewA);

                Assert.Throws<InvalidOperationException>(() => mapping.AddObjectPair(source, previewB));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(previewA);
                UnityEngine.Object.DestroyImmediate(previewB);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void AddObjectPair_WhenPreviewIsRebound_ThrowsInvalidOperationException ()
        {
            var mapping = new TemporaryMirrorMapping();
            var sourceA = new GameObject("SourceA");
            var sourceB = new GameObject("SourceB");
            var preview = new GameObject("Preview");
            try
            {
                mapping.AddObjectPair(sourceA, preview);

                Assert.Throws<InvalidOperationException>(() => mapping.AddObjectPair(sourceB, preview));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sourceA);
                UnityEngine.Object.DestroyImmediate(sourceB);
                UnityEngine.Object.DestroyImmediate(preview);
            }
        }
    }
}
