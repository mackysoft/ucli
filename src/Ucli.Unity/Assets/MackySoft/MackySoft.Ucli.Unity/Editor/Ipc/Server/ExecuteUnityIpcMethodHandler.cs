using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles <c>execute</c> IPC method requests. </summary>
    internal sealed class ExecuteUnityIpcMethodHandler : IUnityIpcMethodHandler
    {
        private readonly IExecuteRequestDispatcher executeRequestDispatcher;

        /// <summary> Initializes a new instance of the <see cref="ExecuteUnityIpcMethodHandler" /> class. </summary>
        /// <param name="executeRequestDispatcher"> The execute-request dispatcher dependency. </param>
        public ExecuteUnityIpcMethodHandler (IExecuteRequestDispatcher executeRequestDispatcher)
        {
            this.executeRequestDispatcher = executeRequestDispatcher ?? throw new ArgumentNullException(nameof(executeRequestDispatcher));
        }

        /// <inheritdoc />
        public string Method => IpcMethodNames.Execute;

        /// <inheritdoc />
        public async ValueTask<IpcResponse> Handle (
            IpcRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(request);

            if (!UnityIpcRequestCodec.TryDecodeExecuteRequest(
                    request,
                    out IpcExecuteRequest? executeRequest,
                    out var errorResponse))
            {
                return errorResponse!;
            }

            var context = new ExecuteDispatchContext(
                RequestId: request.RequestId,
                ProtocolVersion: request.ProtocolVersion);
            return await executeRequestDispatcher.Dispatch(executeRequest!, context, cancellationToken);
        }
    }
}
