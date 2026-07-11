using MackySoft.Ucli.Unity.ScreenshotCapture;
using NUnit.Framework;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityScreenshotGameViewPresentationMappingTests
    {
        [Test]
        [Category("Size.Small")]
        public void TryResolveSourceUvTransform_WithUnflippedPresentation_ReturnsIdentity ()
        {
            var target = new Rect(-12f, 24f, 1920f, 1080f);

            var result = UnityScreenshotGameViewPresentationMapping.TryResolveSourceUvTransform(
                target,
                target,
                out var transform,
                out var errorMessage);

            Assert.That(result, Is.True, errorMessage);
            Assert.That(transform, Is.EqualTo(new Vector4(1f, 1f, 0f, 0f)));
        }

        [Test]
        [Category("Size.Small")]
        public void TryResolveSourceUvTransform_WithDeviceFlippedPresentation_ReturnsVerticalFlip ()
        {
            var target = new Rect(-12f, 24f, 1920f, 1080f);
            var deviceFlipped = new Rect(-12f, 1104f, 1920f, -1080f);

            var result = UnityScreenshotGameViewPresentationMapping.TryResolveSourceUvTransform(
                target,
                deviceFlipped,
                out var transform,
                out var errorMessage);

            Assert.That(result, Is.True, errorMessage);
            Assert.That(transform, Is.EqualTo(new Vector4(1f, -1f, 0f, 1f)));
        }

        [Test]
        [Category("Size.Small")]
        public void TryResolveSourceUvTransform_WithUnknownMapping_FailsClosed ()
        {
            var result = UnityScreenshotGameViewPresentationMapping.TryResolveSourceUvTransform(
                new Rect(0f, 0f, 1920f, 1080f),
                new Rect(0f, 500f, 1920f, -1080f),
                out _,
                out _);

            Assert.That(result, Is.False);
        }
    }
}
