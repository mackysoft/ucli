using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles <c>test.run</c> IPC method requests. </summary>
    internal sealed class TestRunUnityIpcMethodHandler : IStreamingUnityIpcMethodHandler
    {
        private readonly IUnityTestRunService testRunService;

        /// <summary> Initializes a new instance of the <see cref="TestRunUnityIpcMethodHandler" /> class. </summary>
        /// <param name="testRunService"> The test-run service dependency. </param>
        public TestRunUnityIpcMethodHandler (IUnityTestRunService testRunService)
        {
            this.testRunService = testRunService ?? throw new ArgumentNullException(nameof(testRunService));
        }

        /// <inheritdoc />
        public string Method => IpcMethodNames.TestRun;

        /// <inheritdoc />
        public async ValueTask<IpcResponse> HandleAsync (
            IpcRequest request,
            CancellationToken cancellationToken)
        {
            return await HandleCoreAsync(request, progressSink: null, cancellationToken);
        }

        /// <inheritdoc />
        public async ValueTask<IpcResponse> HandleStreamingAsync (
            IpcRequest request,
            IUnityIpcStreamFrameWriter streamWriter,
            CancellationToken cancellationToken)
        {
            if (streamWriter == null)
            {
                throw new ArgumentNullException(nameof(streamWriter));
            }

            return await HandleCoreAsync(request, streamWriter, cancellationToken);
        }

        private async ValueTask<IpcResponse> HandleCoreAsync (
            IpcRequest request,
            IUnityTestRunProgressSink progressSink,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!UnityIpcRequestCodec.TryDecodeTestRunRequest(
                    request,
                    out IpcTestRunRequest? testRunRequest,
                    out var errorResponse))
            {
                return errorResponse!;
            }

            return await HandleDecodedAsync(request, testRunRequest!, progressSink, cancellationToken);
        }

        private async ValueTask<IpcResponse> HandleCoreAsync (
            IpcRequest request,
            IUnityIpcStreamFrameWriter streamWriter,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!UnityIpcRequestCodec.TryDecodeTestRunRequest(
                    request,
                    out IpcTestRunRequest? testRunRequest,
                    out var errorResponse))
            {
                return errorResponse!;
            }

            return await HandleDecodedAsync(
                request,
                testRunRequest!,
                new UnityIpcTestRunProgressSink(streamWriter, testRunRequest!.RunId, cancellationToken),
                cancellationToken);
        }

        private async ValueTask<IpcResponse> HandleDecodedAsync (
            IpcRequest request,
            IpcTestRunRequest testRunRequest,
            IUnityTestRunProgressSink progressSink,
            CancellationToken cancellationToken)
        {
            IpcResponse response;
            try
            {
                var result = await testRunService.ExecuteAsync(
                    testRunRequest,
                    progressSink,
                    cancellationToken);

                if (!result.IsSuccess)
                {
                    var error = result.Error!;
                    response = UnityIpcResponseFactory.CreateErrorResponse(
                        request,
                        error.Code,
                        error.Message,
                        error.OpId);
                    return await FlushAndReturnAsync(request, response, progressSink, cancellationToken);
                }

                response = UnityIpcResponseFactory.CreateSuccessResponse(request, result.Payload!);
                return await FlushAndReturnAsync(request, response, progressSink, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ArgumentException exception)
            {
                response = UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InvalidArgument,
                    exception.Message,
                    null);
                return await FlushAndReturnAsync(request, response, progressSink, cancellationToken);
            }
            catch (Exception exception)
            {
                response = UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InternalError,
                    $"Unity test run failed. {exception.Message}",
                    null);
                return await FlushAndReturnAsync(request, response, progressSink, cancellationToken);
            }
        }

        private static async ValueTask<IpcResponse> FlushAndReturnAsync (
            IpcRequest request,
            IpcResponse response,
            IUnityTestRunProgressSink progressSink,
            CancellationToken cancellationToken)
        {
            if (progressSink != null)
            {
                try
                {
                    await progressSink.FlushAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    return UnityIpcResponseFactory.CreateErrorResponse(
                        request,
                        UcliCoreErrorCodes.InternalError,
                        $"Unity test-run progress stream failed. {exception.Message}",
                        null);
                }
            }

            return response;
        }
    }
}
