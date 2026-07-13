using MackySoft.Ucli.Unity.ScreenshotCapture.Capture;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityScreenshotRuntimeCapabilityPolicyTests
    {
        [Test]
        [Category("Size.Small")]
        public void TryValidateEnvironment_WithCurrentCapturePath_ReturnsTrue ()
        {
            var result = UnityScreenshotRuntimeCapabilityPolicy.TryValidateEnvironment(
                RuntimePlatform.OSXEditor,
                GraphicsDeviceType.Metal,
                ColorSpace.Linear,
                out var errorMessage);

            Assert.That(result, Is.True, errorMessage);
        }

        [TestCase(RuntimePlatform.WindowsEditor, GraphicsDeviceType.Metal, ColorSpace.Linear, Category = "Size.Small")]
        [TestCase(RuntimePlatform.LinuxEditor, GraphicsDeviceType.Metal, ColorSpace.Linear, Category = "Size.Small")]
        [TestCase(RuntimePlatform.OSXEditor, GraphicsDeviceType.OpenGLCore, ColorSpace.Linear, Category = "Size.Small")]
        [TestCase(RuntimePlatform.OSXEditor, GraphicsDeviceType.Metal, ColorSpace.Gamma, Category = "Size.Small")]
        public void TryValidateEnvironment_OutsideCurrentCapturePath_ReturnsFalse (
            RuntimePlatform platform,
            GraphicsDeviceType graphicsDeviceType,
            ColorSpace colorSpace)
        {
            var result = UnityScreenshotRuntimeCapabilityPolicy.TryValidateEnvironment(
                platform,
                graphicsDeviceType,
                colorSpace,
                out var errorMessage);

            Assert.That(result, Is.False);
            Assert.That(errorMessage, Is.Not.Empty);
        }
    }
}
