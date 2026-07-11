using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.ScreenshotCapture
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
            var stagingPublished = false;
            try
            {
                var backendResult = await captureBackend.CaptureAsync(request, cancellationToken);
                if (!backendResult.IsSuccess)
                {
                    return UnityScreenshotCaptureResult.Failure(
                        backendResult.ErrorCode!.Value,
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

                if (!long.TryParse(
                    before.DomainReloadGeneration,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var domainReloadGeneration))
                {
                    return UnityScreenshotCaptureResult.Failure(
                        ScreenshotErrorCodes.ScreenshotCaptureUnsupported,
                        "Unity Editor domain-reload generation could not be represented by the screenshot contract.");
                }

                var playModeState = before.PlayMode?.State;
                if (string.IsNullOrWhiteSpace(playModeState))
                {
                    return UnityScreenshotCaptureResult.Failure(
                        ScreenshotErrorCodes.ScreenshotCaptureUnsupported,
                        "Unity Editor Play Mode state was unavailable at the screenshot capture boundary.");
                }

                var frame = backendResult.Frame;
                var expectedSizeBytes = checked((long)frame.Width * frame.Height * 4L);
                if (frame.Rgba8SrgbTopDown == null
                    || frame.Rgba8SrgbTopDown.LongLength != expectedSizeBytes)
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

                var sizeMode = request.RequestedWidth.HasValue
                    ? IpcScreenshotSizeModeNames.RequestedResolution
                    : IpcScreenshotSizeModeNames.CurrentSurface;
                var response = new IpcScreenshotCaptureResponse(
                    new IpcScreenshotCapture(
                        Target: request.Target,
                        SizeMode: sizeMode,
                        RequestedWidth: request.RequestedWidth,
                        RequestedHeight: request.RequestedHeight,
                        Width: frame.Width,
                        Height: frame.Height,
                        ColorSpace: frame.ColorSpace,
                        LifecycleStateAtCapture: before.LifecycleState,
                        CompileStateAtCapture: before.CompileState,
                        DomainReloadGeneration: domainReloadGeneration,
                        PlayModeState: playModeState),
                    new IpcScreenshotStagingImage(
                        Path: request.StagingPath,
                        PixelFormat: IpcScreenshotPixelFormatNames.Rgba8Srgb,
                        RowOrder: IpcScreenshotRowOrderNames.TopDown,
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
            return IsCaptureReady(readinessResult.Snapshot)
                ? UnityEditorExecutionReadinessResult.Ready(readinessResult.Snapshot)
                : readinessResult;
        }

        private static bool IsCaptureReady (UnityEditorLifecycleSnapshot snapshot)
        {
            if (snapshot == null || snapshot.EditorMode != DaemonEditorMode.Gui)
            {
                return false;
            }

            if (snapshot.CanAcceptExecutionRequests
                && string.Equals(
                    snapshot.LifecycleState,
                    IpcEditorLifecycleStateCodec.Ready,
                    StringComparison.Ordinal))
            {
                return true;
            }

            var playMode = snapshot.PlayMode;
            return string.Equals(
                    snapshot.LifecycleState,
                    IpcEditorLifecycleStateCodec.Playmode,
                    StringComparison.Ordinal)
                && playMode != null
                && playMode.IsPlaying
                && string.Equals(
                    playMode.State,
                    ContractLiteralCodec.ToValue(IpcPlayModeState.Playing),
                    StringComparison.Ordinal)
                && string.Equals(
                    playMode.Transition,
                    ContractLiteralCodec.ToValue(IpcPlayModeTransition.None),
                    StringComparison.Ordinal);
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
                && string.Equals(before.LifecycleState, after.LifecycleState, StringComparison.Ordinal)
                && string.Equals(before.CompileState, after.CompileState, StringComparison.Ordinal)
                && string.Equals(before.CompileGeneration, after.CompileGeneration, StringComparison.Ordinal)
                && string.Equals(before.AssetRefreshGeneration, after.AssetRefreshGeneration, StringComparison.Ordinal)
                && string.Equals(before.DomainReloadGeneration, after.DomainReloadGeneration, StringComparison.Ordinal)
                && string.Equals(before.PlayMode?.State, after.PlayMode?.State, StringComparison.Ordinal)
                && string.Equals(before.PlayMode?.Transition, after.PlayMode?.Transition, StringComparison.Ordinal)
                && string.Equals(before.PlayMode?.Generation, after.PlayMode?.Generation, StringComparison.Ordinal)
                && before.PlayMode?.IsPlaying == after.PlayMode?.IsPlaying
                && before.PlayMode?.IsPlayingOrWillChangePlaymode == after.PlayMode?.IsPlayingOrWillChangePlaymode;
        }
    }
}
