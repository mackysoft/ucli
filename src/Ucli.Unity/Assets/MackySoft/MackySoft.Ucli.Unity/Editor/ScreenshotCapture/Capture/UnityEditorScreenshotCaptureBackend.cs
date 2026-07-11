using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
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

            if (request.RequestedWidth.HasValue != request.RequestedHeight.HasValue
                || request.RequestedWidth.HasValue
                && (request.RequestedWidth.Value <= 0 || request.RequestedHeight.Value <= 0))
            {
                return UnityScreenshotBackendResult.Failure(
                    UcliCoreErrorCodes.InvalidArgument,
                    "Screenshot requested dimensions must be positive and specified together.");
            }

            if (!ContractLiteralCodec.TryParse<IpcScreenshotTarget>(request.Target, out var target))
            {
                return UnityScreenshotBackendResult.Failure(
                    UcliCoreErrorCodes.InvalidArgument,
                    $"Unsupported screenshot target: {request.Target}.");
            }

            if (!UnityScreenshotRuntimeCapabilityPolicy.TryValidateCurrentEnvironment(
                out var capabilityError))
            {
                return Unsupported(capabilityError);
            }

            if (!TryCaptureStablePresentationState(
                out var presentationState,
                out var presentationError))
            {
                return Unsupported(presentationError);
            }

            var result = target switch
            {
                IpcScreenshotTarget.Game => await gameViewCapture.CaptureAsync(
                    request,
                    presentationState,
                    cancellationToken),
                IpcScreenshotTarget.Scene => sceneViewCapture.Capture(
                    request,
                    cancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(target), target, null),
            };
            if (!result.IsSuccess)
            {
                return result;
            }

            return TryValidatePresentationState(presentationState, out presentationError)
                ? result
                : Unsupported(presentationError);
        }

        private static bool TryCaptureStablePresentationState (
            out UnityScreenshotPresentationStateFence.PresentationState state,
            out string errorMessage)
        {
            return UnityScreenshotPresentationStateFence.TryCaptureCurrent(
                    out state,
                    out errorMessage)
                && UnityScreenshotPresentationStateFence.TryValidateObservation(
                    state,
                    out errorMessage);
        }

        private static bool TryValidatePresentationState (
            UnityScreenshotPresentationStateFence.PresentationState expected,
            out string errorMessage)
        {
            if (!UnityScreenshotPresentationStateFence.TryCaptureCurrent(
                out var current,
                out errorMessage))
            {
                return false;
            }

            return UnityScreenshotPresentationStateFence.TryValidateStable(
                expected,
                current,
                out errorMessage);
        }

        private static UnityScreenshotBackendResult Unsupported (string message)
        {
            return UnityScreenshotBackendResult.Failure(
                ScreenshotErrorCodes.ScreenshotCaptureUnsupported,
                message);
        }
    }
}
