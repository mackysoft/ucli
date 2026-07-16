using System;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.ScreenshotCapture;
using MackySoft.Ucli.Unity.ScreenshotCapture.Capture;
using MackySoft.Ucli.Unity.ScreenshotCapture.Pixels;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityScreenshotFrameTests
    {
        [Test]
        [Category("Size.Small")]
        public void Constructor_WithValidRaster_PreservesFrameValues ()
        {
            var rgba8SrgbTopDown = new byte[] { 1, 2, 3, 255, 4, 5, 6, 255 };

            var frame = new UnityScreenshotFrame(
                width: 2,
                height: 1,
                IpcScreenshotColorSpace.Linear,
                rgba8SrgbTopDown);

            Assert.That(frame.Width, Is.EqualTo(2));
            Assert.That(frame.Height, Is.EqualTo(1));
            Assert.That(frame.ColorSpace, Is.EqualTo(IpcScreenshotColorSpace.Linear));
            Assert.That(frame.Rgba8SrgbTopDown, Is.EqualTo(rgba8SrgbTopDown.AsMemory()));
        }

        [Test]
        [Category("Size.Small")]
        [TestCase(0, 1)]
        [TestCase(1, 0)]
        [TestCase(IpcScreenshotCaptureLimits.MaximumDimension + 1, 1)]
        [TestCase(1, IpcScreenshotCaptureLimits.MaximumDimension + 1)]
        public void Constructor_WithDimensionOutsideContract_Throws (
            int width,
            int height)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new UnityScreenshotFrame(
                    width,
                    height,
                    IpcScreenshotColorSpace.Linear,
                    ReadOnlyMemory<byte>.Empty));
        }

        [Test]
        [Category("Size.Small")]
        public void Constructor_WithRasterLargerThanContract_Throws ()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new UnityScreenshotFrame(
                    width: IpcScreenshotCaptureLimits.MaximumDimension,
                    height: IpcScreenshotCaptureLimits.MaximumDimension,
                    IpcScreenshotColorSpace.Linear,
                    ReadOnlyMemory<byte>.Empty));
        }

        [Test]
        [Category("Size.Small")]
        public void Constructor_WithUndefinedColorSpace_Throws ()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new UnityScreenshotFrame(
                    width: 1,
                    height: 1,
                    (IpcScreenshotColorSpace)999,
                    new byte[4]));
        }

        [Test]
        [Category("Size.Small")]
        [TestCase(7)]
        [TestCase(9)]
        public void Constructor_WithMismatchedByteLength_Throws (int byteLength)
        {
            Assert.Throws<ArgumentException>(() =>
                new UnityScreenshotFrame(
                    width: 2,
                    height: 1,
                    IpcScreenshotColorSpace.Linear,
                    new byte[byteLength]));
        }
    }
}
