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

        private readonly IUnityEditorReadinessGate readinessGate;

        /// <summary> Initializes a new instance of the <see cref="ExecuteUnityIpcMethodHandler" /> class. </summary>
        /// <param name="executeRequestDispatcher"> The execute-request dispatcher dependency. </param>
        /// <param name="readinessGate"> The editor-readiness gate dependency. </param>
        public ExecuteUnityIpcMethodHandler (
            IExecuteRequestDispatcher executeRequestDispatcher,
            IUnityEditorReadinessGate readinessGate)
        {
            this.executeRequestDispatcher = executeRequestDispatcher ?? throw new ArgumentNullException(nameof(executeRequestDispatcher));
            this.readinessGate = readinessGate ?? throw new ArgumentNullException(nameof(readinessGate));
        }

        /// <inheritdoc />
        public string Method => IpcMethodNames.Execute;

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

            if (!UnityIpcRequestCodec.TryDecodeExecuteRequest(
                    request,
                    out IpcExecuteRequest? executeRequest,
                    out var errorResponse))
            {
                return errorResponse!;
            }

            await readinessGate.WaitUntilReady(cancellationToken);

            var context = new ExecuteDispatchContext(
                RequestId: request.RequestId,
                ProtocolVersion: request.ProtocolVersion);
            return await executeRequestDispatcher.Dispatch(executeRequest!, context, cancellationToken);
        }
    }
}