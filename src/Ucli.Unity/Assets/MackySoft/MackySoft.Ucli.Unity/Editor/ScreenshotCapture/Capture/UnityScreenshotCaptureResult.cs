using System;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.ScreenshotCapture.Capture
{
    /// <summary> Represents one Unity-side screenshot capture result. </summary>
    internal sealed record UnityScreenshotCaptureResult
    {
        private UnityScreenshotCaptureResult (
            IpcScreenshotCaptureResponse response,
            IpcError error)
        {
            Response = response;
            Error = error;
        }

        /// <summary> Gets a value indicating whether capture succeeded. </summary>
        public bool IsSuccess => Response != null;

        /// <summary> Gets the capture response on success; otherwise <see langword="null" />. </summary>
        public IpcScreenshotCaptureResponse Response { get; }

        /// <summary> Gets the capture error on failure; otherwise <see langword="null" />. </summary>
        public IpcError Error { get; }

        /// <summary> Creates a successful capture result. </summary>
        public static UnityScreenshotCaptureResult Success (IpcScreenshotCaptureResponse response)
        {
            return new UnityScreenshotCaptureResult(
                response ?? throw new ArgumentNullException(nameof(response)),
                error: null);
        }

        /// <summary> Creates a failed capture result. </summary>
        public static UnityScreenshotCaptureResult Failure (
            UcliCode code,
            string message)
        {
            if (!code.IsValid)
            {
                throw new ArgumentException("Screenshot capture error code must be valid.", nameof(code));
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("Screenshot capture error message must not be empty.", nameof(message));
            }

            return new UnityScreenshotCaptureResult(
                response: null,
                new IpcError(code, message, OpId: null));
        }
    }
}
