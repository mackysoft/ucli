using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Implements stream-level request-response exchange handling for Unity IPC connections. </summary>
    internal sealed class UnityIpcConnectionHandler : IUnityIpcConnectionHandler
    {
        private readonly IUnityIpcRequestProcessor requestProcessor;

        private readonly IDaemonShutdownSignal daemonShutdownSignal;

        /// <summary> Initializes a new instance of the <see cref="UnityIpcConnectionHandler" /> class. </summary>
        /// <param name="requestProcessor"> The shared IPC request-processor dependency. </param>
        /// <param name="daemonShutdownSignal"> The daemon shutdown signal dependency. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="requestProcessor" /> is <see langword="null" />. </exception>
        public UnityIpcConnectionHandler (
            IUnityIpcRequestProcessor requestProcessor,
            IDaemonShutdownSignal daemonShutdownSignal)
        {
            this.requestProcessor = requestProcessor ?? throw new ArgumentNullException(nameof(requestProcessor));
            this.daemonShutdownSignal = daemonShutdownSignal ?? throw new ArgumentNullException(nameof(daemonShutdownSignal));
        }

        /// <summary> Handles one request-response exchange over a connected transport stream. </summary>
        /// <param name="stream"> The connected transport stream. </param>
        /// <param name="cancellationToken"> The cancellation token for request handling. </param>
        /// <returns> The handled connection exchange result. </returns>
        /// <exception cref="OperationCanceledException"> Thrown when operation is canceled. </exception>
        public async Task<UnityIpcConnectionHandleResult> HandleAsync (
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

                return default;
            }

            var request = readResult.Value;
            var response = await requestProcessor.ProcessAsync(request, cancellationToken);
            await IpcFrameCodec.WriteModelAsync(
                stream,
                response,
                IpcJsonSerializerOptions.Default,
                cancellationToken: cancellationToken);

            if (ShouldSignalShutdown(request, response))
            {
                daemonShutdownSignal.Signal();
            }

            return new UnityIpcConnectionHandleResult(request, response);
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
