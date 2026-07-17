using MackySoft.Ucli.Unity.ScreenshotCapture.SceneView;
using NUnit.Framework;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnitySceneViewPresentationMappingTests
    {
        [TestCase(1f, 100, 50)]
        [TestCase(2f, 200, 100)]
        [Category("Size.Small")]
        public void TryResolve_WithFullWindow_MapsBackingScale (
            float backingScale,
            int expectedWidth,
            int expectedHeight)
        {
            var result = UnitySceneViewPresentationMapping.TryResolve(
                new Rect(0f, 0f, 100f, 50f),
                new Rect(0f, 0f, 100f, 50f),
                backingScale,
                out var mapping,
                out var errorMessage);

            Assert.That(result, Is.True, errorMessage);
            Assert.That(mapping.FramebufferWidth, Is.EqualTo(expectedWidth));
            Assert.That(mapping.FramebufferHeight, Is.EqualTo(expectedHeight));
            Assert.That(mapping.ContentWidth, Is.EqualTo(expectedWidth));
            Assert.That(mapping.ContentHeight, Is.EqualTo(expectedHeight));
            Assert.That(mapping.SourceUvTransform, Is.EqualTo(new Vector4(1f, -1f, 0f, 1f)));
        }

        [Test]
        [Category("Size.Small")]
        public void TryResolve_WithBottomOriginViewport_MapsTopOriginGrabPixelsUvTransform ()
        {
            var result = UnitySceneViewPresentationMapping.TryResolve(
                new Rect(0f, 0f, 200f, 100f),
                new Rect(10f, 20f, 100f, 50f),
                backingScale: 2f,
                out var mapping,
                out var errorMessage);

            Assert.That(result, Is.True, errorMessage);
            Assert.That(mapping.FramebufferWidth, Is.EqualTo(400));
            Assert.That(mapping.FramebufferHeight, Is.EqualTo(200));
            Assert.That(mapping.ContentWidth, Is.EqualTo(200));
            Assert.That(mapping.ContentHeight, Is.EqualTo(100));
            Assert.That(
                mapping.SourceUvTransform,
                Is.EqualTo(new Vector4(0.5f, -0.5f, 0.05f, 0.8f)));
        }

        [Test]
        [Category("Size.Small")]
        public void TryResolve_WithFractionalEdges_RoundsEachPhysicalEdgeDeterministically ()
        {
            var result = UnitySceneViewPresentationMapping.TryResolve(
                new Rect(0f, 0f, 10.2f, 8.2f),
                new Rect(0.3f, 0.4f, 4.4f, 3.4f),
                backingScale: 2f,
                out var mapping,
                out var errorMessage);

            Assert.That(result, Is.True, errorMessage);
            Assert.That(mapping.FramebufferWidth, Is.EqualTo(20));
            Assert.That(mapping.FramebufferHeight, Is.EqualTo(16));
            Assert.That(mapping.ContentWidth, Is.EqualTo(8));
            Assert.That(mapping.ContentHeight, Is.EqualTo(7));
            Assert.That(
                mapping.SourceUvTransform,
                Is.EqualTo(new Vector4(0.4f, -0.4375f, 0.05f, 0.9375f)));
        }

        [TestCase(0f, 100f, 0f, 0f, 100f, 50f, 1f)]
        [TestCase(100f, 50f, -1f, 0f, 100f, 50f, 1f)]
        [TestCase(100f, 50f, 0f, 0f, 101f, 50f, 1f)]
        [TestCase(100f, 50f, 0f, 0f, 100f, 50f, 0f)]
        [Category("Size.Small")]
        public void TryResolve_WithInvalidOrOutOfBoundsMapping_FailsClosed (
            float windowWidth,
            float windowHeight,
            float contentX,
            float contentY,
            float contentWidth,
            float contentHeight,
            float backingScale)
        {
            var result = UnitySceneViewPresentationMapping.TryResolve(
                new Rect(0f, 0f, windowWidth, windowHeight),
                new Rect(contentX, contentY, contentWidth, contentHeight),
                backingScale,
                out _,
                out var errorMessage);

            Assert.That(result, Is.False);
            Assert.That(errorMessage, Is.Not.Empty);
        }
    }
}
