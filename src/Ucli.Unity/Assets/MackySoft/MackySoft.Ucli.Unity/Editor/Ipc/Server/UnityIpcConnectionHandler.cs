using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Implements stream-level request-response exchange handling for Unity IPC connections. </summary>
    internal sealed class UnityIpcConnectionHandler : IUnityIpcConnectionHandler
    {
        private readonly IUnityIpcRequestHandler requestHandler;

        /// <summary> Initializes a new instance of the <see cref="UnityIpcConnectionHandler" /> class. </summary>
        /// <param name="requestHandler"> The IPC request-handler dependency. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="requestHandler" /> is <see langword="null" />. </exception>
        public UnityIpcConnectionHandler (IUnityIpcRequestHandler requestHandler)
        {
            this.requestHandler = requestHandler ?? throw new ArgumentNullException(nameof(requestHandler));
        }

        /// <summary> Handles one request-response exchange over a connected transport stream. </summary>
        /// <param name="stream"> The connected transport stream. </param>
        /// <param name="cancellationToken"> The cancellation token for request handling. </param>
        /// <returns> A task that completes after one response frame is written. </returns>
        /// <exception cref="OperationCanceledException"> Thrown when operation is canceled. </exception>
        public async Task Handle (
            Stream stream,
            CancellationToken cancellationToken = default)
        {
            IpcRequest request;
            try
            {
                request = await UnityIpcFrameCodec.ReadModel<IpcRequest>(
                    stream,
                    UnityIpcSerializerOptions.Default,
                    cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                var errorResponse = UnityIpcResponseFactory.CreateMalformedFrameResponse(exception);
                await UnityIpcFrameCodec.WriteModel(
                    stream,
                    errorResponse,
                    UnityIpcSerializerOptions.Default,
                    cancellationToken: cancellationToken);
                return;
            }

            var response = await requestHandler.Handle(request, cancellationToken);
            await UnityIpcFrameCodec.WriteModel(
                stream,
                response,
                UnityIpcSerializerOptions.Default,
                cancellationToken: cancellationToken);
        }
    }
}
