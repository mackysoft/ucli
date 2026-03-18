using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Processes IPC requests on the Unity main thread through the shared request handler. </summary>
    internal sealed class UnityIpcRequestProcessor : IUnityIpcRequestProcessor
    {
        private readonly IUnityIpcRequestHandler requestHandler;

        private readonly IUnityMainThreadRequestExecutor mainThreadRequestExecutor;

        /// <summary> Initializes a new instance of the <see cref="UnityIpcRequestProcessor" /> class. </summary>
        /// <param name="requestHandler"> The shared IPC request handler dependency. </param>
        /// <param name="mainThreadRequestExecutor"> The Unity main-thread request executor dependency. </param>
        /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
        public UnityIpcRequestProcessor (
            IUnityIpcRequestHandler requestHandler,
            IUnityMainThreadRequestExecutor mainThreadRequestExecutor)
        {
            this.requestHandler = requestHandler ?? throw new ArgumentNullException(nameof(requestHandler));
            this.mainThreadRequestExecutor = mainThreadRequestExecutor ?? throw new ArgumentNullException(nameof(mainThreadRequestExecutor));
        }

        /// <summary> Processes one IPC request through the shared Unity request pipeline. </summary>
        /// <param name="request"> The incoming IPC request envelope. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by host execution. </param>
        /// <returns> The processed IPC response envelope. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="request" /> is <see langword="null" />. </exception>
        public Task<IpcResponse> Process (
            IpcRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            cancellationToken.ThrowIfCancellationRequested();
            return mainThreadRequestExecutor.Execute(
                () => requestHandler.Handle(request, cancellationToken),
                cancellationToken);
        }
    }
}