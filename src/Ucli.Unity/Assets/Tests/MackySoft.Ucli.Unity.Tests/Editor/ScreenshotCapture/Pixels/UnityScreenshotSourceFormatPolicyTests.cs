using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.ScreenshotCapture.Pixels;
using NUnit.Framework;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityScreenshotSourceFormatPolicyTests
    {
        [TestCase(GraphicsFormat.R8G8B8A8_SRGB, Category = "Size.Small")]
        [TestCase(GraphicsFormat.B8G8R8A8_SRGB, Category = "Size.Small")]
        public void TryValidateGameViewSource_WithSupportedLinearFormat_ReturnsTrue (
            GraphicsFormat graphicsFormat)
        {
            var result = UnityScreenshotSourceFormatPolicy.TryValidateGameViewSource(
                graphicsFormat,
                TextureDimension.Tex2D,
                antiAliasing: 1,
                IpcScreenshotColorSpace.Linear,
                out var errorMessage);

            Assert.That(result, Is.True, errorMessage);
        }

        [TestCase(GraphicsFormat.R8G8B8A8_UNorm, Category = "Size.Small")]
        [TestCase(GraphicsFormat.B8G8R8A8_UNorm, Category = "Size.Small")]
        public void TryValidateGameViewSource_WithSupportedGammaFormat_ReturnsTrue (
            GraphicsFormat graphicsFormat)
        {
            var result = UnityScreenshotSourceFormatPolicy.TryValidateGameViewSource(
                graphicsFormat,
                TextureDimension.Tex2D,
                antiAliasing: 1,
                IpcScreenshotColorSpace.Gamma,
                out var errorMessage);

            Assert.That(result, Is.True, errorMessage);
        }

        [TestCase(GraphicsFormat.R16G16B16A16_SFloat, TextureDimension.Tex2D, 1, Category = "Size.Small")]
        [TestCase(GraphicsFormat.R8G8B8A8_SRGB, TextureDimension.Tex2D, 2, Category = "Size.Small")]
        [TestCase(GraphicsFormat.R8G8B8A8_SRGB, TextureDimension.Tex2DArray, 1, Category = "Size.Small")]
        public void TryValidateGameViewSource_WithUnsupportedFormatContract_ReturnsFalse (
            GraphicsFormat graphicsFormat,
            TextureDimension dimension,
            int antiAliasing)
        {
            var result = UnityScreenshotSourceFormatPolicy.TryValidateGameViewSource(
                graphicsFormat,
                dimension,
                antiAliasing,
                IpcScreenshotColorSpace.Linear,
                out var errorMessage);

            Assert.That(result, Is.False);
            Assert.That(errorMessage, Is.Not.Empty);
        }

        [TestCase(GraphicsFormat.R8G8B8A8_SRGB, IpcScreenshotColorSpace.Gamma, Category = "Size.Small")]
        [TestCase(GraphicsFormat.R8G8B8A8_UNorm, IpcScreenshotColorSpace.Linear, Category = "Size.Small")]
        public void TryValidateGameViewSource_WithMismatchedColorSpaceAndFormat_ReturnsFalse (
            GraphicsFormat graphicsFormat,
            IpcScreenshotColorSpace colorSpace)
        {
            var result = UnityScreenshotSourceFormatPolicy.TryValidateGameViewSource(
                graphicsFormat,
                TextureDimension.Tex2D,
                antiAliasing: 1,
                colorSpace,
                out var errorMessage);

            Assert.That(result, Is.False);
            Assert.That(errorMessage, Does.Contain("active color space"));
        }

        [Test]
        [Category("Size.Small")]
        public void TryValidateSceneFramebufferFormat_WithNativeBgraSrgb_ReturnsTrue ()
        {
            var result = UnityScreenshotSourceFormatPolicy.TryValidateSceneFramebufferFormat(
                GraphicsFormat.B8G8R8A8_SRGB,
                out var errorMessage);

            Assert.That(result, Is.True, errorMessage);
        }

        [Test]
        [Category("Size.Small")]
        public void TryValidateSceneFramebufferFormat_WithNativeBgraUnorm_ReturnsTrue ()
        {
            var result = UnityScreenshotSourceFormatPolicy.TryValidateSceneFramebufferFormat(
                GraphicsFormat.B8G8R8A8_UNorm,
                out var errorMessage);

            Assert.That(result, Is.True, errorMessage);
        }

        [TestCase(GraphicsFormat.R8G8B8A8_SRGB, Category = "Size.Small")]
        [TestCase(GraphicsFormat.R16G16B16A16_SFloat, Category = "Size.Small")]
        public void TryValidateSceneFramebufferFormat_WithUnsafeNativeFormat_ReturnsFalse (
            GraphicsFormat graphicsFormat)
        {
            var result = UnityScreenshotSourceFormatPolicy.TryValidateSceneFramebufferFormat(
                graphicsFormat,
                out var errorMessage);

            Assert.That(result, Is.False);
            Assert.That(errorMessage, Does.Contain("incompatible"));
        }
    }
}
