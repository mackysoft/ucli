using System;

namespace MackySoft.Ucli.Unity.ScreenshotCapture.Pixels
{
    /// <summary> Represents normalized screenshot pixels or one pixel-processing failure. </summary>
    internal sealed class UnityScreenshotNormalizationResult
    {
        private UnityScreenshotNormalizationResult (UnityScreenshotFrame frame, string errorMessage)
        {
            Frame = frame;
            ErrorMessage = errorMessage;
        }

        public UnityScreenshotFrame Frame { get; }

        public string ErrorMessage { get; }

        public bool IsSuccess => Frame != null;

        public static UnityScreenshotNormalizationResult Success (UnityScreenshotFrame frame)
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
    }
}
