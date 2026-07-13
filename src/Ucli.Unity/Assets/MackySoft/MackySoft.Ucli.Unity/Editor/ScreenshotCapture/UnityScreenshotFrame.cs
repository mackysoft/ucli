using System;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Unity.ScreenshotCapture
{
    /// <summary> Represents one validated top-down RGBA8 sRGB screenshot raster. </summary>
    internal sealed class UnityScreenshotFrame
    {
        /// <summary> Initializes a screenshot frame that satisfies the raw-image contract. </summary>
        public UnityScreenshotFrame (
            int width,
            int height,
            IpcScreenshotColorSpace colorSpace,
            ReadOnlyMemory<byte> rgba8SrgbTopDown)
        {
            if (width <= 0 || width > IpcScreenshotCaptureLimits.MaximumDimension)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(width),
                    width,
                    $"Screenshot width must be between 1 and {IpcScreenshotCaptureLimits.MaximumDimension} pixels.");
            }

            if (height <= 0 || height > IpcScreenshotCaptureLimits.MaximumDimension)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(height),
                    height,
                    $"Screenshot height must be between 1 and {IpcScreenshotCaptureLimits.MaximumDimension} pixels.");
            }

            var expectedSizeBytes = checked((long)width * height * 4L);
            if (expectedSizeBytes > IpcScreenshotCaptureLimits.MaximumRawImageBytes)
            {
                throw new ArgumentException(
                    $"Screenshot raster must not exceed {IpcScreenshotCaptureLimits.MaximumRawImageBytes} bytes.");
            }

            if (!ContractLiteralCodec.IsDefined(colorSpace))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(colorSpace),
                    colorSpace,
                    "Unsupported screenshot color space.");
            }

            if (rgba8SrgbTopDown.Length != expectedSizeBytes)
            {
                throw new ArgumentException(
                    $"Screenshot RGBA8 byte length must equal width * height * 4 ({expectedSizeBytes}).",
                    nameof(rgba8SrgbTopDown));
            }

            Width = width;
            Height = height;
            ColorSpace = colorSpace;
            Rgba8SrgbTopDown = rgba8SrgbTopDown;
        }

        /// <summary> Gets the frame width in pixels. </summary>
        public int Width { get; }

        /// <summary> Gets the frame height in pixels. </summary>
        public int Height { get; }

        /// <summary> Gets the Unity project color space used while producing the presentation pixels. </summary>
        public IpcScreenshotColorSpace ColorSpace { get; }

        /// <summary> Gets the top-down RGBA8 sRGB pixel buffer. </summary>
        public ReadOnlyMemory<byte> Rgba8SrgbTopDown { get; }
    }
}
