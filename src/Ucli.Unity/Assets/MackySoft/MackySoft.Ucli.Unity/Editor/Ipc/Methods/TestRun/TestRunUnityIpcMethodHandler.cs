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
                progressSinkFactory: null,
                cancellationToken);
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
                executionCancellationToken => new UnityIpcTestRunProgressSink(
                    streamWriter,
                    testRunRequest!.RunId,
                    executionCancellationToken,
                    cancellationToken),
                cancellationToken);
        }

        private async ValueTask<IpcResponse> HandleDecodedAsync (
            IpcRequest request,
            IpcTestRunRequest testRunRequest,
            Func<CancellationToken, IUnityTestRunProgressSink> progressSinkFactory,
            CancellationToken cancellationToken)
        {
            IpcResponse response;
            IUnityTestRunProgressSink progressSink = null;
            CancellationTokenSource requestTimeoutCancellationTokenSource = null;
            try
            {
                if (!TryValidateTimeoutMilliseconds(testRunRequest.TimeoutMilliseconds, out var timeoutErrorMessage))
                {
                    response = UnityIpcResponseFactory.CreateErrorResponse(
                        request,
                        UcliCoreErrorCodes.InvalidArgument,
                        timeoutErrorMessage,
                        null);
                    return response;
                }

                requestTimeoutCancellationTokenSource = CreateRequestTimeoutCancellationTokenSource(
                    testRunRequest.TimeoutMilliseconds,
                    cancellationToken);
                var executionCancellationToken = requestTimeoutCancellationTokenSource != null
                    ? requestTimeoutCancellationTokenSource.Token
                    : cancellationToken;
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
                    return await FlushAndReturnAsync(request, response, progressSink, cancellationToken);
                }

                response = UnityIpcResponseFactory.CreateSuccessResponse(request, result.Payload!);
                return await FlushAndReturnAsync(request, response, progressSink, cancellationToken);
            }
            catch (OperationCanceledException) when (IsRequestTimeout(
                requestTimeoutCancellationTokenSource,
                cancellationToken))
            {
                response = UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    IpcTransportErrorCodes.IpcTimeout,
                    $"Unity test run timed out after {testRunRequest.TimeoutMilliseconds.Value} milliseconds.",
                    null);
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
            finally
            {
                requestTimeoutCancellationTokenSource?.Dispose();
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

        private static bool TryValidateTimeoutMilliseconds (
            int? timeoutMilliseconds,
            out string errorMessage)
        {
            errorMessage = null;
            if (!timeoutMilliseconds.HasValue || timeoutMilliseconds.Value > 0)
            {
                return true;
            }

            errorMessage = "Test run timeoutMilliseconds must be greater than zero when specified.";
            return false;
        }

        private static CancellationTokenSource CreateRequestTimeoutCancellationTokenSource (
            int? timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            if (!timeoutMilliseconds.HasValue)
            {
                return null;
            }

            var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cancellationTokenSource.CancelAfter(timeoutMilliseconds.Value);
            return cancellationTokenSource;
        }

        private static bool IsRequestTimeout (
            CancellationTokenSource requestTimeoutCancellationTokenSource,
            CancellationToken callerCancellationToken)
        {
            return requestTimeoutCancellationTokenSource != null
                && requestTimeoutCancellationTokenSource.IsCancellationRequested
                && !callerCancellationToken.IsCancellationRequested;
        }
    }
}
