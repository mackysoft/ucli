using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
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

            var before = readinessResult.Snapshot;
            if (before.PlayMode == null)
            {
                return UnityScreenshotCaptureResult.Failure(
                    ScreenshotErrorCodes.ScreenshotCaptureUnsupported,
                    "Unity Editor Play Mode state was not available for screenshot metadata.");
            }

            var stagingPublished = false;
            try
            {
                var backendResult = await captureBackend.CaptureAsync(request, cancellationToken);
                if (!backendResult.IsSuccess)
                {
                    if (!backendResult.ErrorCode.HasValue)
                    {
                        throw new InvalidOperationException(
                            "Screenshot backend failure did not provide an error code.");
                    }

                    return UnityScreenshotCaptureResult.Failure(
                        backendResult.ErrorCode.Value,
                        backendResult.ErrorMessage);
                }

                cancellationToken.ThrowIfCancellationRequested();
                var after = readinessGate.CaptureSnapshot();
                if (!IsSameCaptureFence(before, after))
                {
                    return UnityScreenshotCaptureResult.Failure(
                        ScreenshotErrorCodes.ScreenshotCaptureUnsupported,
                        "Unity Editor lifecycle state changed while screenshot pixels were captured.");
                }

                var frame = backendResult.Frame;
                var expectedSizeBytes = checked((long)frame.Width * frame.Height * 4L);
                if (frame.Rgba8SrgbTopDown.Length != expectedSizeBytes)
                {
                    return UnityScreenshotCaptureResult.Failure(
                        UcliCoreErrorCodes.InternalError,
                        "Screenshot backend returned a raw staging buffer with an invalid byte count.");
                }

                var sizeBytes = await stagingImageWriter.WriteAtomicAsync(
                    request.StagingPath,
                    frame.Rgba8SrgbTopDown,
                    cancellationToken);
                stagingPublished = true;
                cancellationToken.ThrowIfCancellationRequested();
                if (sizeBytes != expectedSizeBytes)
                {
                    stagingImageWriter.DeleteIfExists(request.StagingPath);
                    stagingPublished = false;
                    return UnityScreenshotCaptureResult.Failure(
                        UcliCoreErrorCodes.InternalError,
                        "Screenshot staging writer returned a byte count that does not match the captured raster.");
                }

                var sizeMode = ContractLiteralCodec.ToValue(request.RequestedWidth.HasValue
                    ? IpcScreenshotSizeMode.RequestedResolution
                    : IpcScreenshotSizeMode.CurrentSurface);
                var response = new IpcScreenshotCaptureResponse(
                    new IpcScreenshotCapture(
                        Target: request.Target,
                        SizeMode: sizeMode,
                        RequestedWidth: request.RequestedWidth,
                        RequestedHeight: request.RequestedHeight,
                        Width: frame.Width,
                        Height: frame.Height,
                        ColorSpace: frame.ColorSpace,
                        LifecycleStateAtCapture: ContractLiteralCodec.ToValue(before.LifecycleState),
                        CompileStateAtCapture: ContractLiteralCodec.ToValue(before.CompileState),
                        DomainReloadGeneration: before.DomainReloadGeneration,
                        PlayModeState: ContractLiteralCodec.ToValue(before.PlayMode.State)),
                    new IpcScreenshotStagingImage(
                        Path: request.StagingPath,
                        PixelFormat: ContractLiteralCodec.ToValue(IpcScreenshotPixelFormat.Rgba8Srgb),
                        RowOrder: ContractLiteralCodec.ToValue(IpcScreenshotRowOrder.TopDown),
                        RowStrideBytes: checked(frame.Width * 4),
                        SizeBytes: sizeBytes));
                return UnityScreenshotCaptureResult.Success(response);
            }
            catch
            {
                if (stagingPublished)
                {
                    stagingImageWriter.DeleteIfExists(request.StagingPath);
                }

                throw;
            }
        }

        private async Task<UnityEditorExecutionReadinessResult> EnsureCaptureReadyAsync (
            CancellationToken cancellationToken)
        {
            var snapshot = readinessGate.CaptureSnapshot();
            if (snapshot.EditorMode != DaemonEditorMode.Gui)
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
            if (IsCaptureReady(readinessResult.Snapshot))
            {
                return UnityEditorExecutionReadinessResult.Ready(readinessResult.Snapshot);
            }

            return readinessResult;
        }

        private static bool IsCaptureReady (UnityEditorLifecycleSnapshot snapshot)
        {
            if (snapshot == null || snapshot.EditorMode != DaemonEditorMode.Gui)
            {
                return false;
            }

            return snapshot.LifecycleState == IpcEditorLifecycleState.Ready;
        }

        private static bool IsSameCaptureFence (
            UnityEditorLifecycleSnapshot before,
            UnityEditorLifecycleSnapshot after)
        {
            if (before == null || after == null)
            {
                return false;
            }

            return before.EditorMode == after.EditorMode
                && before.LifecycleState == after.LifecycleState
                && before.CompileState == after.CompileState
                && before.CompileGeneration == after.CompileGeneration
                && before.AssetRefreshGeneration == after.AssetRefreshGeneration
                && before.DomainReloadGeneration == after.DomainReloadGeneration
                && before.PlayMode == after.PlayMode;
        }
    }
}
