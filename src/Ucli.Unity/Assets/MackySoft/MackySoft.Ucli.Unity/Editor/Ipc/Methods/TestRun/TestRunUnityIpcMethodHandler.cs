using System;
using MackySoft.Ucli.Contracts;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles <c>test.run</c> IPC method requests. </summary>
    internal sealed class TestRunUnityIpcMethodHandler : IUnityIpcMethodHandler
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
        public async ValueTask<IpcResponse> Handle (
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

            try
            {
                var result = await testRunService.Execute(testRunRequest!, cancellationToken);
                if (!result.IsSuccess)
                {
                    var error = result.Error!;
                    return UnityIpcResponseFactory.CreateErrorResponse(
                        request,
                        error.Code,
                        error.Message,
                        error.OpId);
                }

                return UnityIpcResponseFactory.CreateSuccessResponse(request, result.Payload!);
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
                    null);
            }
            catch (Exception exception)
            {
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InternalError,
                    $"Unity test run failed. {exception.Message}",
                    null);
            }
        }
    }
}
