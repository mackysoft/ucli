using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.ScreenshotCapture.Capture;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles GUI Editor <c>screenshot.capture</c> IPC requests. </summary>
    internal sealed class ScreenshotCaptureUnityIpcMethodHandler : IUnityIpcMethodHandler
    {
        private readonly IUnityScreenshotCaptureService captureService;

        private readonly IIpcRequestTimeoutScopeFactory timeoutScopeFactory;

        /// <summary> Initializes a new screenshot capture method handler. </summary>
        public ScreenshotCaptureUnityIpcMethodHandler (
            IUnityScreenshotCaptureService captureService,
            IIpcRequestTimeoutScopeFactory timeoutScopeFactory)
        {
            this.captureService = captureService ?? throw new ArgumentNullException(nameof(captureService));
            this.timeoutScopeFactory = timeoutScopeFactory ?? throw new ArgumentNullException(nameof(timeoutScopeFactory));
        }

        /// <inheritdoc />
        public UnityIpcMethod Method => UnityIpcMethod.ScreenshotCapture;

        /// <inheritdoc />
        public async ValueTask<IpcResponse> HandleAsync (
            IpcRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!UnityIpcRequestCodec.TryDecodeScreenshotCaptureRequest(
                request,
                out IpcScreenshotCaptureRequest screenshotRequest,
                out var errorResponse))
            {
                return errorResponse;
            }

            if (!TryValidateRequest(screenshotRequest, out var validationError))
            {
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InvalidArgument,
                    validationError,
                    opId: null);
            }

            IIpcRequestTimeoutScope timeoutScope = null;
            try
            {
                timeoutScope = timeoutScopeFactory.CreateLinked(
                    screenshotRequest.TimeoutMilliseconds,
                    cancellationToken);
                var captureResult = await captureService.CaptureAsync(
                    screenshotRequest,
                    timeoutScope.Token);
                return captureResult.IsSuccess
                    ? UnityIpcResponseFactory.CreateSuccessResponse(request, captureResult.Response)
                    : UnityIpcResponseFactory.CreateErrorResponse(
                        request,
                        captureResult.Error.Code,
                        captureResult.Error.Message,
                        captureResult.Error.OpId);
            }
            catch (OperationCanceledException) when (IsRequestTimeout(timeoutScope, cancellationToken))
            {
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    IpcTransportErrorCodes.IpcTimeout,
                    $"Unity screenshot capture timed out after {screenshotRequest.TimeoutMilliseconds} milliseconds.",
                    opId: null);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ArgumentException exception)
            {
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InvalidArgument,
                    exception.Message,
                    opId: null);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InternalError,
                    $"Screenshot staging image could not be written. {exception.Message}",
                    opId: null);
            }
            catch (Exception exception)
            {
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InternalError,
                    $"Unity screenshot capture failed. {exception.Message}",
                    opId: null);
            }
            finally
            {
                timeoutScope?.Dispose();
            }
        }

        private static bool TryValidateRequest (
            IpcScreenshotCaptureRequest request,
            out string errorMessage)
        {
            if (request.RequestedWidth.HasValue != request.RequestedHeight.HasValue)
            {
                errorMessage = "Screenshot requestedWidth and requestedHeight must be specified together.";
                return false;
            }

            if (request.RequestedWidth.HasValue
                && (request.RequestedWidth.Value <= 0 || request.RequestedHeight!.Value <= 0))
            {
                errorMessage = "Screenshot requested dimensions must be positive when specified.";
                return false;
            }

            if (request.RequestedWidth.HasValue
                && !AreRequestedDimensionsWithinContractLimit(
                    request.RequestedWidth.Value,
                    request.RequestedHeight!.Value))
            {
                errorMessage =
                    $"Screenshot requested dimensions must not exceed {IpcScreenshotCaptureLimits.MaximumDimension} pixels per axis "
                    + $"or {IpcScreenshotCaptureLimits.MaximumRawImageBytes} uncompressed bytes.";
                return false;
            }

            if (request.Target == IpcScreenshotTarget.Scene
                && request.RequestedWidth.HasValue)
            {
                errorMessage = "Requested screenshot dimensions are supported only for the game target.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.StagingPath) || !Path.IsPathRooted(request.StagingPath))
            {
                errorMessage = "Screenshot stagingPath must be an absolute path.";
                return false;
            }

            if (request.TimeoutMilliseconds <= 0)
            {
                errorMessage = "Screenshot timeoutMilliseconds must be greater than zero.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        private static bool AreRequestedDimensionsWithinContractLimit (int width, int height)
        {
            if (width > IpcScreenshotCaptureLimits.MaximumDimension
                || height > IpcScreenshotCaptureLimits.MaximumDimension)
            {
                return false;
            }

            try
            {
                return checked((long)width * height * 4L)
                    <= IpcScreenshotCaptureLimits.MaximumRawImageBytes;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        private static bool IsRequestTimeout (
            IIpcRequestTimeoutScope timeoutScope,
            CancellationToken cancellationToken)
        {
            return timeoutScope != null
                && timeoutScope.IsTimeoutCancellationRequested
                && !cancellationToken.IsCancellationRequested;
        }
    }
}
