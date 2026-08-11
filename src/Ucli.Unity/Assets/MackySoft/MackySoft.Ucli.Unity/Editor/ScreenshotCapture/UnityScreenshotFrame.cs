using System;
using MackySoft.Text.Vocabularies;
using TextVocabulary = MackySoft.Text.Vocabularies.Vocabulary;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Presentation;
using MackySoft.Ucli.Contracts.Projects;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Unity.ScreenshotCapture
{
    /// <summary> Represents one validated top-down RGBA8 sRGB screenshot raster. </summary>
    internal sealed class UnityScreenshotFrame
    {
        /// <summary> Initializes a screenshot frame that satisfies the raw-image contract. </summary>
        /// <param name="dimensions"> The positive frame dimensions within the supported screenshot limits. </param>
        /// <param name="projectColorSpace"> The defined Unity project color space used to produce the pixels. </param>
        /// <param name="ownedRgba8SrgbTopDown">
        /// The exact top-down RGBA8 sRGB pixel buffer whose ownership transfers to the frame. The caller must not modify
        /// the underlying storage after construction.
        /// </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="dimensions" /> is <see langword="null" />. </exception>
        /// <exception cref="ArgumentOutOfRangeException"> Thrown when the dimensions exceed the supported layout or <paramref name="projectColorSpace" /> is undefined. </exception>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="ownedRgba8SrgbTopDown" /> does not contain exactly four bytes per pixel. </exception>
        public UnityScreenshotFrame (
            PixelDimensions dimensions,
            UnityProjectColorSpace projectColorSpace,
            ReadOnlyMemory<byte> ownedRgba8SrgbTopDown)
        {
            if (dimensions == null)
            {
                throw new ArgumentNullException(nameof(dimensions));
            }

            if (!IpcScreenshotCaptureLimits.TryCalculateRgba8Layout(
                dimensions.Width,
                dimensions.Height,
                out _,
                out var expectedSizeBytes))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(dimensions),
                    "Screenshot dimensions exceed the supported normalized RGBA8 layout.");
            }

            if (!TextVocabulary.IsDefined(projectColorSpace))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(projectColorSpace),
                    projectColorSpace,
                    "Unsupported Unity project color space.");
            }

            if (ownedRgba8SrgbTopDown.Length != expectedSizeBytes)
            {
                throw new ArgumentException(
                    $"Screenshot RGBA8 byte length must equal width * height * 4 ({expectedSizeBytes}).",
                    nameof(ownedRgba8SrgbTopDown));
            }

            Dimensions = dimensions;
            ProjectColorSpace = projectColorSpace;
            Rgba8SrgbTopDown = ownedRgba8SrgbTopDown;
        }

        /// <summary> Gets the frame dimensions. </summary>
        public PixelDimensions Dimensions { get; }

        /// <summary> Gets the Unity project color space used while producing the presentation pixels. </summary>
        public UnityProjectColorSpace ProjectColorSpace { get; }

        /// <summary> Gets a read-only view of the owned top-down RGBA8 sRGB pixel buffer. </summary>
        public ReadOnlyMemory<byte> Rgba8SrgbTopDown { get; }
    }
}
