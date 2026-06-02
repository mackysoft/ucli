using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
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
                catch (Exception writeException) when (IpcConnectionWriteFailureClassifier.IsConnectionLocalWriteFailure(writeException))
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
                using var requestCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var streamWriter = new IpcStreamFrameWriter(
                    stream,
                    request,
                    _ => TryCancel(requestCancellationTokenSource));
                var streamingResponse = await ProcessStreamingSafelyAsync(
                    request,
                    streamWriter,
                    requestCancellationTokenSource,
                    cancellationToken);
                await WriteTerminalSafelyAsync(streamWriter, streamingResponse, cancellationToken);
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
            return ContractLiteralCodec.TryParse<IpcResponseMode>(request.ResponseMode, out var responseMode)
                && responseMode == IpcResponseMode.Stream;
        }

        private async Task<IpcResponse> ProcessStreamingSafelyAsync (
            IpcRequest request,
            IIpcStreamFrameWriter streamWriter,
            CancellationTokenSource requestCancellationTokenSource,
            CancellationToken connectionCancellationToken)
        {
            try
            {
                return await requestProcessor.ProcessStreamingAsync(
                    request,
                    streamWriter,
                    requestCancellationTokenSource.Token);
            }
            catch (OperationCanceledException) when (!connectionCancellationToken.IsCancellationRequested
                && requestCancellationTokenSource.IsCancellationRequested)
            {
                return CreateStreamWriteFailureResponse(request, "Streaming IPC request was canceled because the response stream failed.");
            }
            catch (Exception exception) when (requestCancellationTokenSource.IsCancellationRequested
                && IpcConnectionWriteFailureClassifier.IsConnectionLocalWriteFailure(exception))
            {
                return CreateStreamWriteFailureResponse(request, $"Streaming IPC response stream failed. {exception.Message}");
            }
        }

        private static IpcResponse CreateStreamWriteFailureResponse (
            IpcRequest request,
            string message)
        {
            return UnityIpcResponseFactory.CreateErrorResponse(
                request,
                UcliCoreErrorCodes.InternalError,
                message,
                null);
        }

        private static async Task WriteTerminalSafelyAsync (
            IIpcStreamFrameWriter streamWriter,
            IpcResponse response,
            CancellationToken cancellationToken)
        {
            try
            {
                await streamWriter.WriteTerminalAsync(response, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (IpcConnectionWriteFailureClassifier.IsConnectionLocalWriteFailure(exception))
            {
                // NOTE:
                // A broken response stream means the peer has already lost the connection.
                // Keep the failure connection-local so the daemon listener can keep serving.
            }
        }

        private static void TryCancel (CancellationTokenSource cancellationTokenSource)
        {
            try
            {
                cancellationTokenSource.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }
}
