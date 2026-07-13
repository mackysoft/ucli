using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.ScreenshotCapture.GameView;
using MackySoft.Ucli.Unity.ScreenshotCapture.SceneView;

namespace MackySoft.Ucli.Unity.ScreenshotCapture.Capture
{
    /// <summary> Routes screenshot requests while enforcing the presentation-state evidence fence. </summary>
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

            if (!UnityScreenshotRuntimeCapabilityPolicy.TryValidateCurrentEnvironment(
                out var capabilityError))
            {
                return Unsupported(capabilityError);
            }

            if (!UnityScreenshotPresentationStateFence.TryCaptureStable(
                out var presentationState,
                out var presentationError))
            {
                return Unsupported(presentationError);
            }

            var result = request.Target switch
            {
                IpcScreenshotTarget.Game => await gameViewCapture.CaptureAsync(
                    request,
                    presentationState,
                    cancellationToken),
                IpcScreenshotTarget.Scene => sceneViewCapture.Capture(
                    request,
                    cancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(request.Target), request.Target, null),
            };
            if (!result.IsSuccess)
            {
                return result;
            }

            return UnityScreenshotPresentationStateFence.TryValidateCurrentStable(
                presentationState,
                out presentationError)
                ? result
                : Unsupported(presentationError);
        }

        private static UnityScreenshotBackendResult Unsupported (string message)
        {
            return UnityScreenshotBackendResult.Failure(
                ScreenshotErrorCodes.ScreenshotCaptureUnsupported,
                message);
        }
    }
}
