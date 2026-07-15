using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;

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
        public UnityIpcMethod Method => UnityIpcMethod.TestRun;

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
                progressSinkFactory: null,
                cancellation);
        }

        /// <inheritdoc />
        public async ValueTask<IpcResponse> HandleStreamingAsync (
            ValidatedUnityIpcRequest request,
            IIpcStreamFrameWriter streamWriter,
            IpcRequestCancellation cancellation)
        {
            if (streamWriter == null)
            {
                throw new ArgumentNullException(nameof(streamWriter));
            }

            cancellation.Token.ThrowIfCancellationRequested();
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
                executionCancellationToken => new UnityIpcTestRunProgressSink(
                    streamWriter,
                    testRunRequest!.RunId,
                    executionCancellationToken),
                cancellation);
        }

        private async ValueTask<IpcResponse> HandleDecodedAsync (
            ValidatedUnityIpcRequest request,
            IpcTestRunRequest testRunRequest,
            Func<CancellationToken, UnityIpcTestRunProgressSink> progressSinkFactory,
            IpcRequestCancellation cancellation)
        {
            IpcResponse response;
            UnityIpcTestRunProgressSink progressSink = null;
            try
            {
                var executionCancellationToken = cancellation.Token;
                progressSink = progressSinkFactory?.Invoke(executionCancellationToken);
                var result = await testRunService.ExecuteAsync(
                    testRunRequest,
                    progressSink,
                    executionCancellationToken);

                if (!result.IsSuccess)
                {
                    var error = result.Error!;
                    response = UnityIpcResponseFactory.CreateErrorResponse(
                        request,
                        error.Code,
                        error.Message,
                        error.OpId);
                    return await CompleteProgressAndReturnAsync(
                        request,
                        response,
                        progressSink,
                        cancellation);
                }

                response = UnityIpcResponseFactory.CreateSuccessResponse(request, result.Payload!);
                return await CompleteProgressAndReturnAsync(
                    request,
                    response,
                    progressSink,
                    cancellation);
            }
            catch (OperationCanceledException) when (cancellation.Reason == IpcRequestCancellationReason.ExecutionDeadline)
            {
                response = CreateTimeoutResponse(request);
                return await CompleteProgressAndReturnAsync(
                    request,
                    response,
                    progressSink,
                    cancellation);
            }
            catch (OperationCanceledException)
            {
                if (progressSink != null)
                {
                    try
                    {
                        await progressSink.CompleteAndFlushAsync(
                            cancellation.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        // Completion already sealed progress acceptance and observes any active drain failure.
                    }
                }

                throw;
            }
            catch (ArgumentException exception)
            {
                response = UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InvalidArgument,
                    exception.Message,
                    null);
                return await CompleteProgressAndReturnAsync(
                    request,
                    response,
                    progressSink,
                    cancellation);
            }
            catch (Exception exception)
            {
                response = UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InternalError,
                    $"Unity test run failed. {exception.Message}",
                    null);
                return await CompleteProgressAndReturnAsync(
                    request,
                    response,
                    progressSink,
                    cancellation);
            }
        }

        private static async ValueTask<IpcResponse> CompleteProgressAndReturnAsync (
            ValidatedUnityIpcRequest request,
            IpcResponse response,
            UnityIpcTestRunProgressSink progressSink,
            IpcRequestCancellation cancellation)
        {
            if (progressSink != null)
            {
                try
                {
                    await progressSink.CompleteAndFlushAsync(
                        cancellation.Token);
                }
                catch (OperationCanceledException) when (cancellation.Reason == IpcRequestCancellationReason.ExecutionDeadline)
                {
                    return CreateTimeoutResponse(request);
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

        private static IpcResponse CreateTimeoutResponse (ValidatedUnityIpcRequest request)
        {
            return UnityIpcResponseFactory.CreateErrorResponse(
                request,
                IpcTransportErrorCodes.IpcTimeout,
                "Unity test run reached its request deadline.",
                null);
        }
    }
}
