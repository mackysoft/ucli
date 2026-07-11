using System;

namespace MackySoft.Ucli.Unity.ScreenshotCapture.Pixels
{
    /// <summary> Represents normalized screenshot pixels or one pixel-processing failure. </summary>
    internal sealed class UnityScreenshotNormalizationResult
    {
        private UnityScreenshotNormalizationResult (NormalizedFrame frame, string errorMessage)
        {
            Frame = frame;
            ErrorMessage = errorMessage;
        }

        public NormalizedFrame Frame { get; }

        public string ErrorMessage { get; }

        public bool IsSuccess => Frame != null;

        public static UnityScreenshotNormalizationResult Success (NormalizedFrame frame)
        {
            return new UnityScreenshotNormalizationResult(
                frame ?? throw new ArgumentNullException(nameof(frame)),
                errorMessage: null);
        }

        public static UnityScreenshotNormalizationResult Failure (string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                throw new ArgumentException(
                    "Screenshot normalization error message must not be empty.",
                    nameof(errorMessage));
            }

            return new UnityScreenshotNormalizationResult(frame: null, errorMessage);
        }

        internal sealed record NormalizedFrame (
            int Width,
            int Height,
            string ColorSpace,
            byte[] Rgba8SrgbTopDown);
    }
}
