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
                out var capability,
                out var errorMessage);

            Assert.That(result, Is.True, errorMessage);
            Assert.That(
                capability.FramebufferGraphicsFormat,
                Is.EqualTo(GraphicsFormat.B8G8R8A8_SRGB));
        }

        [Test]
        [Category("Size.Small")]
        public void TryCreateCapability_WithNativeBgraUnorm_ReturnsCapability ()
        {
            var result = UnitySceneViewCaptureCapabilityResolver.TryCreateCapability(
                GraphicsFormat.B8G8R8A8_UNorm,
                GetRequiredGrabPixelsMethod(typeof(SupportedMemberShapeFixture)),
                CreateMapping(),
                out var capability,
                out var errorMessage);

            Assert.That(result, Is.True, errorMessage);
            Assert.That(capability.FramebufferGraphicsFormat, Is.EqualTo(GraphicsFormat.B8G8R8A8_UNorm));
        }

        [TestCase(typeof(StaticMemberShapeFixture))]
        [TestCase(typeof(BoolMemberShapeFixture))]
        [TestCase(typeof(WrongParametersMemberShapeFixture))]
        [Category("Size.Small")]
        public void TryCreateCapability_WithoutExactGrabPixelsShape_FailsClosed (Type fixtureType)
        {
            var result = TryCreate(
                GetRequiredGrabPixelsMethod(fixtureType),
                out _,
                out var errorMessage);

            Assert.That(result, Is.False);
            Assert.That(errorMessage, Does.Contain("exact instance GrabPixels"));
        }

        private static bool TryCreate (
            MethodInfo method,
            out UnitySceneViewCaptureCapability capability,
            out string errorMessage)
        {
            return UnitySceneViewCaptureCapabilityResolver.TryCreateCapability(
                GraphicsFormat.B8G8R8A8_SRGB,
                method,
                CreateMapping(),
                out capability,
                out errorMessage);
        }

        private static UnitySceneViewPresentationMapping CreateMapping ()
        {
            if (!UnitySceneViewPresentationMapping.TryResolve(
                new Rect(0f, 0f, 2f, 2f),
                new Rect(0f, 0f, 2f, 2f),
                backingScale: 1f,
                out var mapping,
                out var errorMessage))
            {
                throw new InvalidOperationException(errorMessage);
            }

            return mapping;
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
