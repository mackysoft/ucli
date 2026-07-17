using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.ScreenshotCapture.GameView;
using MackySoft.Ucli.Unity.ScreenshotCapture.SceneView;

namespace MackySoft.Ucli.Unity.ScreenshotCapture.Capture
{
    /// <summary> Routes screenshot requests to the target-specific capture backend. </summary>
    internal sealed class UnityEditorScreenshotCaptureBackend : IUnityScreenshotCaptureBackend
    {
        private readonly UnityGameViewScreenshotCapture gameViewCapture;

        private readonly UnitySceneViewScreenshotCapture sceneViewCapture;

        public UnityEditorScreenshotCaptureBackend (
            UnityGameViewScreenshotCapture gameViewCapture,
            UnitySceneViewScreenshotCapture sceneViewCapture)
        {
            this.gameViewCapture = gameViewCapture
                ?? throw new ArgumentNullException(nameof(gameViewCapture));
            this.sceneViewCapture = sceneViewCapture
                ?? throw new ArgumentNullException(nameof(sceneViewCapture));
        }

        public async Task<UnityScreenshotBackendResult> CaptureAsync (
            IpcScreenshotCaptureRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            return request.Target switch
            {
                IpcScreenshotTarget.Game => await gameViewCapture.CaptureAsync(
                    request,
                    cancellationToken),
                IpcScreenshotTarget.Scene => sceneViewCapture.Capture(
                    request,
                    cancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(request.Target), request.Target, null),
            };
        }
    }
}
