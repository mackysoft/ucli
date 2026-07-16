using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using MackySoft.Ucli.Unity.ScreenshotCapture.Staging;

namespace MackySoft.Ucli.Unity.ScreenshotCapture.Capture
{
    /// <summary> Coordinates lifecycle fences, pixel capture, and raw staging publication. </summary>
    internal sealed class UnityScreenshotCaptureService : IUnityScreenshotCaptureService
    {
        private readonly IUnityEditorReadinessGate readinessGate;

        private readonly IUnityScreenshotCaptureBackend captureBackend;

        private readonly IScreenshotStagingImageWriter stagingImageWriter;

        /// <summary> Initializes a new screenshot capture service. </summary>
        public UnityScreenshotCaptureService (
            IUnityEditorReadinessGate readinessGate,
            IUnityScreenshotCaptureBackend captureBackend,
            IScreenshotStagingImageWriter stagingImageWriter)
        {
            this.readinessGate = readinessGate ?? throw new ArgumentNullException(nameof(readinessGate));
            this.captureBackend = captureBackend ?? throw new ArgumentNullException(nameof(captureBackend));
            this.stagingImageWriter = stagingImageWriter ?? throw new ArgumentNullException(nameof(stagingImageWriter));
        }

        /// <inheritdoc />
        public async Task<UnityScreenshotCaptureResult> CaptureAsync (
            IpcScreenshotCaptureRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var readinessResult = await EnsureCaptureReadyAsync(cancellationToken);
            if (!readinessResult.IsReady)
            {
                return UnityScreenshotCaptureResult.Failure(
                    readinessResult.Error.Code,
                    readinessResult.Error.Message);
            }

            var before = readinessResult.Observation;
            var stagingPublished = false;
            try
            {
                var backendResult = await captureBackend.CaptureAsync(request, cancellationToken);
                if (!backendResult.IsSuccess)
                {
                    return UnityScreenshotCaptureResult.Failure(
                        backendResult.ErrorCode,
                        backendResult.ErrorMessage);
                }

                cancellationToken.ThrowIfCancellationRequested();
                var after = readinessGate.CaptureObservation();
                if (before.State != after.State)
                {
                    return UnityScreenshotCaptureResult.Failure(
                        ScreenshotErrorCodes.ScreenshotCaptureUnsupported,
                        "Unity Editor lifecycle state changed while screenshot pixels were captured.");
                }

                var frame = backendResult.Frame;
                var sizeBytes = await stagingImageWriter.WriteAtomicAsync(
                    request.CaptureId,
                    frame.Rgba8SrgbTopDown,
                    cancellationToken);
                stagingPublished = true;
                cancellationToken.ThrowIfCancellationRequested();
                if (sizeBytes != frame.Rgba8SrgbTopDown.Length)
                {
                    stagingImageWriter.DeleteIfExists(request.CaptureId);
                    stagingPublished = false;
                    return UnityScreenshotCaptureResult.Failure(
                        UcliCoreErrorCodes.InternalError,
                        "Screenshot staging writer returned a byte count that does not match the captured raster.");
                }

                var sizeMode = request.RequestedWidth.HasValue
                    ? IpcScreenshotSizeMode.RequestedResolution
                    : IpcScreenshotSizeMode.CurrentSurface;
                var response = new IpcScreenshotCaptureResponse(
                    request.CaptureId,
                    new IpcScreenshotCapture(
                        Target: request.Target,
                        SizeMode: sizeMode,
                        RequestedWidth: request.RequestedWidth,
                        RequestedHeight: request.RequestedHeight,
                        Width: frame.Width,
                        Height: frame.Height,
                        ColorSpace: frame.ColorSpace,
                        State: before.State),
                    new IpcScreenshotStagingImage(
                        Width: frame.Width,
                        Height: frame.Height,
                        PixelFormat: IpcScreenshotPixelFormat.Rgba8Srgb,
                        RowOrder: IpcScreenshotRowOrder.TopDown,
                        RowStrideBytes: checked(frame.Width * IpcScreenshotCaptureLimits.Rgba8BytesPerPixel),
                        SizeBytes: sizeBytes));
                return UnityScreenshotCaptureResult.Success(response);
            }
            catch
            {
                if (stagingPublished)
                {
                    stagingImageWriter.DeleteIfExists(request.CaptureId);
                }

                throw;
            }
        }

        private async Task<UnityEditorExecutionReadinessResult> EnsureCaptureReadyAsync (
            CancellationToken cancellationToken)
        {
            var snapshot = readinessGate.CaptureObservation();
            if (snapshot.State.EditorMode != DaemonEditorMode.Gui)
            {
                return UnityEditorExecutionReadinessResult.Blocked(
                    snapshot,
                    new IpcError(
                        ScreenshotErrorCodes.ScreenshotRequiresGuiSession,
                        "Screenshot capture requires a registered GUI Editor session.",
                        OpId: null));
            }

            if (IsCaptureReady(snapshot))
            {
                return UnityEditorExecutionReadinessResult.Ready(snapshot);
            }

            var readinessResult = await readinessGate.EnsureExecutionReadyAsync(
                failFast: false,
                cancellationToken);
            if (IsCaptureReady(readinessResult.Observation))
            {
                return UnityEditorExecutionReadinessResult.Ready(readinessResult.Observation);
            }

            return readinessResult;
        }

        private static bool IsCaptureReady (UnityEditorObservation snapshot)
        {
            if (snapshot == null || snapshot.State.EditorMode != DaemonEditorMode.Gui)
            {
                return false;
            }

            return snapshot.State.LifecycleState == IpcEditorLifecycleState.Ready;
        }

    }
}
