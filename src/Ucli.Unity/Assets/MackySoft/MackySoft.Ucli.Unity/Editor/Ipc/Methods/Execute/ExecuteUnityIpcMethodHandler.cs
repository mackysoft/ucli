using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Dispatch;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles <c>execute</c> IPC method requests. </summary>
    internal sealed class ExecuteUnityIpcMethodHandler : IUnityIpcMethodHandler
    {
        private readonly IExecuteRequestDispatcher executeRequestDispatcher;

        private readonly IIpcRequestTimeoutScopeFactory timeoutScopeFactory;

        private readonly IpcProjectIdentity projectIdentity;

        /// <summary> Initializes a new instance of the <see cref="ExecuteUnityIpcMethodHandler" /> class. </summary>
        /// <param name="executeRequestDispatcher"> The execute-request dispatcher dependency. </param>
        /// <param name="timeoutScopeFactory"> The request timeout-scope factory dependency. </param>
        /// <param name="projectIdentity"> The project identity served by this Unity IPC host. </param>
        public ExecuteUnityIpcMethodHandler (
            IExecuteRequestDispatcher executeRequestDispatcher,
            IIpcRequestTimeoutScopeFactory timeoutScopeFactory,
            IpcProjectIdentity projectIdentity)
        {
            this.executeRequestDispatcher = executeRequestDispatcher ?? throw new ArgumentNullException(nameof(executeRequestDispatcher));
            this.timeoutScopeFactory = timeoutScopeFactory ?? throw new ArgumentNullException(nameof(timeoutScopeFactory));
            this.projectIdentity = projectIdentity ?? throw new ArgumentNullException(nameof(projectIdentity));
        }

        /// <inheritdoc />
        public string Method => IpcMethodNames.Execute;

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

            if (!UnityIpcRequestCodec.TryDecodeExecuteRequest(
                    request,
                    out IpcExecuteRequest? executeRequest,
                    out var errorResponse))
            {
                return errorResponse!;
            }

            var context = new ExecuteDispatchContext(
                RequestId: request.RequestId,
                ProtocolVersion: request.ProtocolVersion)
            {
                Project = projectIdentity,
            };

            if (executeRequest!.TimeoutMilliseconds.HasValue
                && executeRequest.TimeoutMilliseconds.Value <= 0)
            {
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InvalidArgument,
                    "Execute request timeoutMilliseconds must be greater than zero when specified.",
                    null);
            }

            IIpcRequestTimeoutScope requestTimeoutScope = null;
            try
            {
                requestTimeoutScope = timeoutScopeFactory.CreateLinked(executeRequest.TimeoutMilliseconds, cancellationToken);
                return await executeRequestDispatcher.DispatchAsync(executeRequest, context, requestTimeoutScope.Token);
            }
            catch (OperationCanceledException) when (IsRequestTimeout(requestTimeoutScope, cancellationToken))
            {
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    IpcTransportErrorCodes.IpcTimeout,
                    $"Unity execute request timed out after {executeRequest.TimeoutMilliseconds!.Value} milliseconds.",
                    null);
            }
            finally
            {
                requestTimeoutScope?.Dispose();
            }
        }

        private static bool IsRequestTimeout (
            IIpcRequestTimeoutScope requestTimeoutScope,
            CancellationToken cancellationToken)
        {
            return requestTimeoutScope != null
                && requestTimeoutScope.IsTimeoutCancellationRequested
                && !cancellationToken.IsCancellationRequested;
        }
    }
}
