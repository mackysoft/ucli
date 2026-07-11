using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.ScreenshotCapture.Capture
{
    /// <summary> Captures one Unity Editor presentation surface into a host-owned raw staging image. </summary>
    internal interface IUnityScreenshotCaptureService
    {
        /// <summary> Captures the requested presentation surface. </summary>
        /// <param name="request"> The validated screenshot capture request. </param>
        /// <param name="cancellationToken"> The request execution cancellation token. </param>
        /// <returns> The capture response or a structured failure. </returns>
        Task<UnityScreenshotCaptureResult> CaptureAsync (
            IpcScreenshotCaptureRequest request,
            CancellationToken cancellationToken);
    }
}
