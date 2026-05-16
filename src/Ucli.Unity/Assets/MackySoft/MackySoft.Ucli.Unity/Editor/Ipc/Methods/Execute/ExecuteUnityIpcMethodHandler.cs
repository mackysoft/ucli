using System;
using System.Threading;
using System.Threading.Tasks;
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
        public ExecuteUnityIpcMethodHandler (IExecuteRequestDispatcher executeRequestDispatcher)
            : this(executeRequestDispatcher, IpcProjectIdentity.Unknown)
        {
        }

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
            return await executeRequestDispatcher.DispatchAsync(executeRequest!, context, cancellationToken);
        }
    }
}
