using System;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Presentation;
using MackySoft.Ucli.Contracts.Projects;
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
                new PixelDimensions(2, 1),
                UnityProjectColorSpace.Linear,
                rgba8SrgbTopDown);

            Assert.That(frame.Dimensions, Is.EqualTo(new PixelDimensions(2, 1)));
            Assert.That(frame.ProjectColorSpace, Is.EqualTo(UnityProjectColorSpace.Linear));
            Assert.That(frame.Rgba8SrgbTopDown, Is.EqualTo(rgba8SrgbTopDown.AsMemory()));
        }

        [Test]
        [Category("Size.Small")]
        [TestCase(IpcScreenshotCaptureLimits.MaximumDimension + 1, 1)]
        [TestCase(1, IpcScreenshotCaptureLimits.MaximumDimension + 1)]
        public void Constructor_WithDimensionOutsideContract_Throws (
            int width,
            int height)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new UnityScreenshotFrame(
                    new PixelDimensions(width, height),
                    UnityProjectColorSpace.Linear,
                    ReadOnlyMemory<byte>.Empty));
        }

        [Test]
        [Category("Size.Small")]
        public void Constructor_WithRasterLargerThanContract_Throws ()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new UnityScreenshotFrame(
                    new PixelDimensions(
                        IpcScreenshotCaptureLimits.MaximumDimension,
                        IpcScreenshotCaptureLimits.MaximumDimension),
                    UnityProjectColorSpace.Linear,
                    ReadOnlyMemory<byte>.Empty));
        }

        [Test]
        [Category("Size.Small")]
        public void Constructor_WithUndefinedColorSpace_Throws ()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new UnityScreenshotFrame(
                    new PixelDimensions(1, 1),
                    (UnityProjectColorSpace)999,
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
                    new PixelDimensions(2, 1),
                    UnityProjectColorSpace.Linear,
                    new byte[byteLength]));
        }
    }
}
