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

        private readonly IDaemonShutdownSignal daemonShutdownSignal;

        private readonly IUnityMainThreadRequestExecutor mainThreadRequestExecutor;

        /// <summary> Initializes a new instance of the <see cref="UnityIpcConnectionHandler" /> class. </summary>
        /// <param name="requestHandler"> The IPC request-handler dependency. </param>
        /// <param name="daemonShutdownSignal"> The daemon shutdown signal dependency. </param>
        /// <param name="mainThreadRequestExecutor"> The main-thread request-executor dependency. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="requestHandler" /> is <see langword="null" />. </exception>
        public UnityIpcConnectionHandler (
            IUnityIpcRequestHandler requestHandler,
            IDaemonShutdownSignal daemonShutdownSignal,
            IUnityMainThreadRequestExecutor mainThreadRequestExecutor)
        {
            this.requestHandler = requestHandler ?? throw new ArgumentNullException(nameof(requestHandler));
            this.daemonShutdownSignal = daemonShutdownSignal ?? throw new ArgumentNullException(nameof(daemonShutdownSignal));
            this.mainThreadRequestExecutor = mainThreadRequestExecutor ?? throw new ArgumentNullException(nameof(mainThreadRequestExecutor));
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
            var readResult = await IpcFrameCodec.TryReadModelAsync<IpcRequest>(
                stream,
                IpcJsonSerializerOptions.Default,
                cancellationToken: cancellationToken);
            if (!readResult.IsSuccess)
            {
                var errorResponse = UnityIpcResponseFactory.CreateMalformedFrameResponse(
                    readResult.ErrorKind,
                    readResult.ErrorMessage);
                try
                {
                    await IpcFrameCodec.WriteModelAsync(
                        stream,
                        errorResponse,
                        IpcJsonSerializerOptions.Default,
                        cancellationToken: cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception writeException) when (writeException is IOException or ObjectDisposedException or InvalidOperationException)
                {
                    // NOTE:
                    // The peer may already close the connection after sending a malformed frame.
                    // Treat response-write failures as connection-local and do not escalate to listener loop.
                }

                return;
            }

            var request = readResult.Value;
            var response = await mainThreadRequestExecutor.Execute(
                () => requestHandler.Handle(request, cancellationToken),
                cancellationToken);
            await IpcFrameCodec.WriteModelAsync(
                stream,
                response,
                IpcJsonSerializerOptions.Default,
                cancellationToken: cancellationToken);

            if (ShouldSignalShutdown(request, response))
            {
                daemonShutdownSignal.Signal();
            }
        }

        /// <summary> Determines whether shutdown signal should be emitted for one completed response write. </summary>
        /// <param name="request"> The handled IPC request envelope. </param>
        /// <param name="response"> The response that was successfully written. </param>
        /// <returns> <see langword="true" /> when shutdown signal should be emitted; otherwise <see langword="false" />. </returns>
        private static bool ShouldSignalShutdown (
            IpcRequest request,
            IpcResponse response)
        {
            return string.Equals(request.Method, IpcMethodNames.Shutdown, StringComparison.Ordinal)
                && string.Equals(response.Status, IpcProtocol.StatusOk, StringComparison.Ordinal)
                && response.Errors.Count == 0;
        }
    }
}
