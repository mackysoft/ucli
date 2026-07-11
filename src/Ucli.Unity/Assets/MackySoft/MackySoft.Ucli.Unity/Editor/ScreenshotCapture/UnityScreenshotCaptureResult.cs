using System;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.ScreenshotCapture
{
    /// <summary> Represents one Unity-side screenshot capture result. </summary>
    internal sealed record UnityScreenshotCaptureResult (
        IpcScreenshotCaptureResponse Response,
        IpcError Error)
    {
        /// <summary> Gets a value indicating whether capture succeeded. </summary>
        public bool IsSuccess => Response != null && Error == null;

        /// <summary> Creates a successful capture result. </summary>
        public static UnityScreenshotCaptureResult Success (IpcScreenshotCaptureResponse response)
        {
            return new UnityScreenshotCaptureResult(
                response ?? throw new ArgumentNullException(nameof(response)),
                Error: null);
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
                Response: null,
                new IpcError(code, message, OpId: null));
        }
    }
}
