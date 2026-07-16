using System;
using System.IO;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.ScreenshotCapture.Capture;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles GUI Editor <c>screenshot.capture</c> IPC requests. </summary>
    internal sealed class ScreenshotCaptureUnityIpcMethodHandler : IUnityIpcMethodHandler
    {
        private readonly IUnityScreenshotCaptureService captureService;

        private readonly IUnityMutationLaneControl mutationLaneControl;

        /// <summary> Initializes a new screenshot capture method handler. </summary>
        public ScreenshotCaptureUnityIpcMethodHandler (
            IUnityScreenshotCaptureService captureService,
            IUnityMutationLaneControl mutationLaneControl)
        {
            this.captureService = captureService ?? throw new ArgumentNullException(nameof(captureService));
            this.mutationLaneControl = mutationLaneControl ?? throw new ArgumentNullException(nameof(mutationLaneControl));
        }

        /// <inheritdoc />
        public UnityIpcMethod Method => UnityIpcMethod.ScreenshotCapture;

        /// <inheritdoc />
        public async ValueTask<IpcResponse> HandleAsync (
            ValidatedUnityIpcRequest request,
            IpcRequestCancellation cancellation)
        {
            cancellation.Token.ThrowIfCancellationRequested();
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

            try
            {
                var mutationActivity = mutationLaneControl.BeginMutation();
                UnityScreenshotCaptureResult captureResult;
                try
                {
                    captureResult = await captureService.CaptureAsync(
                        screenshotRequest,
                        cancellation.Token);
                }
                finally
                {
                    mutationActivity.Complete();
                }

                return captureResult.IsSuccess
                    ? UnityIpcResponseFactory.CreateSuccessResponse(request, captureResult.Response)
                    : UnityIpcResponseFactory.CreateErrorResponse(
                        request,
                        captureResult.Error.Code,
                        captureResult.Error.Message,
                        captureResult.Error.OpId);
            }
            catch (OperationCanceledException) when (
                cancellation.Reason == IpcRequestCancellationReason.ExecutionDeadline)
            {
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    IpcTransportErrorCodes.IpcTimeout,
                    "Unity screenshot capture reached its request deadline.",
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
        }
    }
}
