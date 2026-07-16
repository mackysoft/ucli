using System;
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

        private readonly IpcProjectIdentity projectIdentity;

        /// <summary> Initializes a new instance of the <see cref="ExecuteUnityIpcMethodHandler" /> class. </summary>
        /// <param name="executeRequestDispatcher"> The execute-request dispatcher dependency. </param>
        /// <param name="projectIdentity"> The project identity served by this Unity IPC host. </param>
        public ExecuteUnityIpcMethodHandler (
            IExecuteRequestDispatcher executeRequestDispatcher,
            IpcProjectIdentity projectIdentity)
        {
            this.executeRequestDispatcher = executeRequestDispatcher ?? throw new ArgumentNullException(nameof(executeRequestDispatcher));
            this.projectIdentity = projectIdentity ?? throw new ArgumentNullException(nameof(projectIdentity));
        }

        /// <inheritdoc />
        public UnityIpcMethod Method => UnityIpcMethod.Execute;

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

            if (!UnityIpcRequestCodec.TryDecodeExecuteRequest(
                    request,
                    out IpcExecuteRequest? executeRequest,
                    out var errorResponse))
            {
                return errorResponse!;
            }

            var context = new ExecuteDispatchContext(
                requestId: request.RequestId,
                project: projectIdentity);

            try
            {
                return await executeRequestDispatcher.DispatchAsync(executeRequest!, context, cancellation.Token);
            }
            catch (OperationCanceledException) when (
                cancellation.Reason == IpcRequestCancellationReason.ExecutionDeadline)
            {
                return UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    IpcTransportErrorCodes.IpcTimeout,
                    "Unity execute request reached its request deadline.",
                    null);
            }
        }
    }
}
