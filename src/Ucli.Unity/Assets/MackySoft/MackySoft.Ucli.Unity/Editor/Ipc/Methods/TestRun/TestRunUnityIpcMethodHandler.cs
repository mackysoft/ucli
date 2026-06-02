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
        private readonly IIpcRequestTimeoutScopeFactory timeoutScopeFactory;

        /// <summary> Initializes a new instance of the <see cref="TestRunUnityIpcMethodHandler" /> class. </summary>
        /// <param name="testRunService"> The test-run service dependency. </param>
        /// <param name="timeoutScopeFactory"> The request timeout scope factory. </param>
        public TestRunUnityIpcMethodHandler (
            IUnityTestRunService testRunService,
            IIpcRequestTimeoutScopeFactory timeoutScopeFactory)
        {
            this.testRunService = testRunService ?? throw new ArgumentNullException(nameof(testRunService));
            this.timeoutScopeFactory = timeoutScopeFactory ?? throw new ArgumentNullException(nameof(timeoutScopeFactory));
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
            IIpcStreamFrameWriter streamWriter,
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
            IIpcRequestTimeoutScope requestTimeoutScope = null;
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

                requestTimeoutScope = timeoutScopeFactory.CreateLinked(
                    testRunRequest.TimeoutMilliseconds,
                    cancellationToken);
                var executionCancellationToken = requestTimeoutScope.Token;
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
                requestTimeoutScope,
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
                requestTimeoutScope?.Dispose();
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

        private static bool IsRequestTimeout (
            IIpcRequestTimeoutScope requestTimeoutScope,
            CancellationToken callerCancellationToken)
        {
            return requestTimeoutScope != null
                && requestTimeoutScope.IsTimeoutCancellationRequested
                && !callerCancellationToken.IsCancellationRequested;
        }
    }
}
