using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.ScreenshotCapture.Capture
{
    /// <summary> Captures and normalizes Unity Editor presentation pixels without committing artifacts. </summary>
    internal interface IUnityScreenshotCaptureBackend
    {
        /// <summary> Captures one target surface as top-down RGBA8 sRGB bytes. </summary>
        Task<UnityScreenshotBackendResult> CaptureAsync (
            IpcScreenshotCaptureRequest request,
            CancellationToken cancellationToken);
    }
}
