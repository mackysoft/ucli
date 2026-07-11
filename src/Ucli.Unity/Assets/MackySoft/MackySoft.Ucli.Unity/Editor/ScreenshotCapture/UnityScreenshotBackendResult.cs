using System;
using MackySoft.Ucli.Contracts;

namespace MackySoft.Ucli.Unity.ScreenshotCapture
{
    /// <summary> Represents normalized pixels or one expected backend failure. </summary>
    internal sealed record UnityScreenshotBackendResult (
        UnityScreenshotBackendResult.CapturedFrame Frame,
        UcliCode? ErrorCode,
        string ErrorMessage)
    {
        /// <summary> Gets a value indicating whether normalized pixels were captured. </summary>
        public bool IsSuccess => Frame != null && ErrorCode == null;

        /// <summary> Creates a successful backend result. </summary>
        public static UnityScreenshotBackendResult Success (CapturedFrame frame)
        {
            return new UnityScreenshotBackendResult(
                frame ?? throw new ArgumentNullException(nameof(frame)),
                ErrorCode: null,
                ErrorMessage: null);
        }

        /// <summary> Creates a failed backend result. </summary>
        public static UnityScreenshotBackendResult Failure (
            UcliCode errorCode,
            string errorMessage)
        {
            if (!errorCode.IsValid)
            {
                throw new ArgumentException("Screenshot backend error code must be valid.", nameof(errorCode));
            }

            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                throw new ArgumentException("Screenshot backend error message must not be empty.", nameof(errorMessage));
            }

            return new UnityScreenshotBackendResult(
                Frame: null,
                errorCode,
                errorMessage);
        }

        /// <summary> Represents one normalized screenshot raster. </summary>
        internal sealed record CapturedFrame (
            int Width,
            int Height,
            string ColorSpace,
            byte[] Rgba8SrgbTopDown);
    }
}
