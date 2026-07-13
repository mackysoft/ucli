using System;
using System.Reflection;
using MackySoft.Ucli.Unity.ScreenshotCapture.SceneView;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnitySceneViewCaptureCapabilityResolverTests
    {
        [Test]
        [Category("Size.Small")]
        public void TryCreateCapability_WithCompleteMeasuredTuple_ReturnsCapability ()
        {
            var result = TryCreate(
                GetRequiredGrabPixelsMethod(typeof(SupportedMemberShapeFixture)),
                sourceStartsAtTop: true,
                out var capability,
                out var errorMessage);

            Assert.That(result, Is.True, errorMessage);
            Assert.That(
                capability.FramebufferGraphicsFormat,
                Is.EqualTo(GraphicsFormat.B8G8R8A8_SRGB));
        }

        [Test]
        [Category("Size.Small")]
        public void TryCreateCapability_WithBottomOriginSource_FailsClosed ()
        {
            var result = TryCreate(
                GetRequiredGrabPixelsMethod(typeof(SupportedMemberShapeFixture)),
                sourceStartsAtTop: false,
                out _,
                out var errorMessage);

            Assert.That(result, Is.False);
            Assert.That(errorMessage, Does.Contain("top-origin"));
        }

        [TestCase(typeof(StaticMemberShapeFixture))]
        [TestCase(typeof(BoolMemberShapeFixture))]
        [TestCase(typeof(WrongParametersMemberShapeFixture))]
        [Category("Size.Small")]
        public void TryCreateCapability_WithoutExactGrabPixelsShape_FailsClosed (Type fixtureType)
        {
            var result = TryCreate(
                GetRequiredGrabPixelsMethod(fixtureType),
                sourceStartsAtTop: true,
                out _,
                out var errorMessage);

            Assert.That(result, Is.False);
            Assert.That(errorMessage, Does.Contain("exact instance GrabPixels"));
        }

        private static bool TryCreate (
            MethodInfo method,
            bool sourceStartsAtTop,
            out UnitySceneViewCaptureCapability capability,
            out string errorMessage)
        {
            var mapping = new UnitySceneViewPresentationMapping(
                FramebufferWidth: 2,
                FramebufferHeight: 2,
                ContentWidth: 2,
                ContentHeight: 2,
                BackingScale: 1f,
                WindowRect: new Rect(0f, 0f, 2f, 2f),
                ContentRect: new Rect(0f, 0f, 2f, 2f),
                SourceUvTransform: new Vector4(1f, -1f, 0f, 1f));
            return UnitySceneViewCaptureCapabilityResolver.TryCreateCapability(
                hdrActive: false,
                sourceStartsAtTop,
                GraphicsFormat.B8G8R8A8_SRGB,
                method,
                mapping,
                out capability,
                out errorMessage);
        }

        private static MethodInfo GetRequiredGrabPixelsMethod (Type fixtureType)
        {
            return fixtureType.GetMethod(
                    "GrabPixels",
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException(
                    $"Test GrabPixels method is unavailable: {fixtureType.FullName}.");
        }

        private sealed class SupportedMemberShapeFixture
        {
            internal void GrabPixels (RenderTexture destination, Rect rectangle)
            {
            }
        }

        private sealed class StaticMemberShapeFixture
        {
            internal static void GrabPixels (RenderTexture destination, Rect rectangle)
            {
            }
        }

        private sealed class BoolMemberShapeFixture
        {
            internal bool GrabPixels (RenderTexture destination, Rect rectangle)
            {
                return true;
            }
        }

        private sealed class WrongParametersMemberShapeFixture
        {
            internal void GrabPixels (RenderTexture destination)
            {
            }
        }
    }
}
