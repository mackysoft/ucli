using System;
using MackySoft.Ucli.Contracts;

namespace MackySoft.Ucli.Unity.ScreenshotCapture.Capture
{
    /// <summary> Represents normalized pixels or one expected backend failure. </summary>
    internal sealed class UnityScreenshotBackendResult
    {
        private UnityScreenshotBackendResult (
            CapturedFrame frame,
            UcliCode? errorCode,
            string errorMessage)
        {
            Frame = frame;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }

        /// <summary> Gets the captured frame when the operation succeeded. </summary>
        public CapturedFrame Frame { get; }

        /// <summary> Gets the structured error code when the operation failed. </summary>
        public UcliCode? ErrorCode { get; }

        /// <summary> Gets the diagnostic error message when the operation failed. </summary>
        public string ErrorMessage { get; }

        /// <summary> Gets a value indicating whether normalized pixels were captured. </summary>
        public bool IsSuccess => Frame != null && ErrorCode == null;

        /// <summary> Creates a successful backend result. </summary>
        public static UnityScreenshotBackendResult Success (CapturedFrame frame)
        {
            return new UnityScreenshotBackendResult(
                frame ?? throw new ArgumentNullException(nameof(frame)),
                errorCode: null,
                errorMessage: null);
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
                frame: null,
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
