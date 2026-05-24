using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Implements stream-level request-response exchange handling for Unity IPC connections. </summary>
    internal sealed class UnityIpcConnectionHandler : IUnityIpcConnectionHandler
    {
        private readonly IUnityIpcRequestProcessor requestProcessor;

        /// <summary> Initializes a new instance of the <see cref="UnityIpcConnectionHandler" /> class. </summary>
        /// <param name="requestProcessor"> The shared IPC request-processor dependency. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="requestProcessor" /> is <see langword="null" />. </exception>
        public UnityIpcConnectionHandler (IUnityIpcRequestProcessor requestProcessor)
        {
            this.requestProcessor = requestProcessor ?? throw new ArgumentNullException(nameof(requestProcessor));
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
            if (IsStreamingResponse(request))
            {
                var streamWriter = new UnityIpcStreamFrameWriter(stream, request);
                var streamingResponse = requestProcessor is IUnityIpcStreamingRequestProcessor streamingRequestProcessor
                    ? await streamingRequestProcessor.ProcessStreamingAsync(
                        request,
                        streamWriter,
                        cancellationToken)
                    : UnityIpcResponseFactory.CreateErrorResponse(
                        request,
                        UcliCoreErrorCodes.InternalError,
                        "Streaming IPC request processor is not registered.",
                        null);
                await streamWriter.WriteTerminalAsync(streamingResponse, cancellationToken);
                return new UnityIpcConnectionHandleResult(request, streamingResponse);
            }

            var response = await requestProcessor.ProcessAsync(request, cancellationToken);
            await IpcFrameCodec.WriteModelAsync(
                stream,
                response,
                IpcJsonSerializerOptions.Default,
                cancellationToken: cancellationToken);

            return new UnityIpcConnectionHandleResult(request, response);
        }

        private static bool IsStreamingResponse (IpcRequest request)
        {
            return string.Equals(request.ResponseMode, IpcResponseModes.Stream, StringComparison.Ordinal);
        }
    }
}
