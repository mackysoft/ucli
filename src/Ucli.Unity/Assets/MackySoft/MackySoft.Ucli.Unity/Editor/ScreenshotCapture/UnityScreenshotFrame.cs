using System;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Unity.ScreenshotCapture
{
    /// <summary> Represents one validated top-down RGBA8 sRGB screenshot raster. </summary>
    internal sealed class UnityScreenshotFrame
    {
        /// <summary> Initializes a screenshot frame that satisfies the raw-image contract. </summary>
        /// <param name="width"> The positive frame width within the supported screenshot limits. </param>
        /// <param name="height"> The positive frame height within the supported screenshot limits. </param>
        /// <param name="colorSpace"> The defined Unity project color space used to produce the pixels. </param>
        /// <param name="ownedRgba8SrgbTopDown">
        /// The exact top-down RGBA8 sRGB pixel buffer whose ownership transfers to the frame. The caller must not modify
        /// the underlying storage after construction.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException"> Thrown when the dimensions exceed the supported layout or <paramref name="colorSpace" /> is undefined. </exception>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="ownedRgba8SrgbTopDown" /> does not contain exactly four bytes per pixel. </exception>
        public UnityScreenshotFrame (
            int width,
            int height,
            IpcScreenshotColorSpace colorSpace,
            ReadOnlyMemory<byte> ownedRgba8SrgbTopDown)
        {
            if (!IpcScreenshotCaptureLimits.TryCalculateRgba8Layout(
                width,
                height,
                out _,
                out var expectedSizeBytes))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(width),
                    "Screenshot dimensions exceed the supported normalized RGBA8 layout.");
            }

            if (!ContractLiteralCodec.IsDefined(colorSpace))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(colorSpace),
                    colorSpace,
                    "Unsupported screenshot color space.");
            }

            if (ownedRgba8SrgbTopDown.Length != expectedSizeBytes)
            {
                throw new ArgumentException(
                    $"Screenshot RGBA8 byte length must equal width * height * 4 ({expectedSizeBytes}).",
                    nameof(ownedRgba8SrgbTopDown));
            }

            Width = width;
            Height = height;
            ColorSpace = colorSpace;
            Rgba8SrgbTopDown = ownedRgba8SrgbTopDown;
        }

        /// <summary> Gets the frame width in pixels. </summary>
        public int Width { get; }

        /// <summary> Gets the frame height in pixels. </summary>
        public int Height { get; }

        /// <summary> Gets the Unity project color space used while producing the presentation pixels. </summary>
        public IpcScreenshotColorSpace ColorSpace { get; }

        /// <summary> Gets a read-only view of the owned top-down RGBA8 sRGB pixel buffer. </summary>
        public ReadOnlyMemory<byte> Rgba8SrgbTopDown { get; }
    }
}
