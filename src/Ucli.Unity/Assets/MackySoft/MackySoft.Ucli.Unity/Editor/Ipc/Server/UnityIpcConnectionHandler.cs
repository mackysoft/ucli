using System;
using System.IO;
using System.IO.Pipes;
using System.Net.Sockets;
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
        internal static readonly TimeSpan DefaultInitialFrameReadTimeout = TimeSpan.FromSeconds(5);

        internal static readonly TimeSpan DefaultResponseFrameWriteTimeout = TimeSpan.FromSeconds(1);

        private readonly IUnityIpcRequestHandler requestHandler;

        private readonly IUnityShutdownAdmissionCoordinator shutdownAdmissionCoordinator;

        private readonly TimeSpan initialFrameReadTimeout;

        private readonly TimeSpan responseFrameWriteTimeout;

        /// <summary> Initializes one connection handler with explicit dependencies and exchange limits. </summary>
        /// <param name="requestHandler"> The shared IPC request-handler dependency. </param>
        /// <param name="shutdownAdmissionCoordinator"> The shutdown exchange admission coordinator. </param>
        /// <param name="initialFrameReadTimeout"> The upper bound for receiving the first request frame. </param>
        /// <param name="responseFrameWriteTimeout"> The upper bound for writing any response frame. </param>
        /// <exception cref="ArgumentNullException"> Thrown when a dependency is <see langword="null" />. </exception>
        /// <exception cref="ArgumentOutOfRangeException"> Thrown when an exchange limit is not positive. </exception>
        public UnityIpcConnectionHandler (
            IUnityIpcRequestHandler requestHandler,
            IUnityShutdownAdmissionCoordinator shutdownAdmissionCoordinator,
            TimeSpan initialFrameReadTimeout,
            TimeSpan responseFrameWriteTimeout)
        {
            this.requestHandler = requestHandler ?? throw new ArgumentNullException(nameof(requestHandler));
            this.shutdownAdmissionCoordinator = shutdownAdmissionCoordinator ?? throw new ArgumentNullException(nameof(shutdownAdmissionCoordinator));
            if (initialFrameReadTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(initialFrameReadTimeout),
                    initialFrameReadTimeout,
                    "Initial frame read timeout must be greater than zero.");
            }

            this.initialFrameReadTimeout = initialFrameReadTimeout;
            if (responseFrameWriteTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(responseFrameWriteTimeout),
                    responseFrameWriteTimeout,
                    "Response frame write timeout must be greater than zero.");
            }

            this.responseFrameWriteTimeout = responseFrameWriteTimeout;
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
            cancellationToken.ThrowIfCancellationRequested();

            IpcFrameReadResult<IpcRequest> readResult;
            using (var initialFrameReadCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                var transportReadCancellationToken = ResolveTransportIoCancellationToken(
                    stream,
                    initialFrameReadCancellationTokenSource.Token);
                var frameReadTask = Task.Run(
                    async () => await IpcFrameCodec.TryReadModelAsync<IpcRequest>(
                            stream,
                            IpcJsonSerializerOptions.Default,
                            cancellationToken: transportReadCancellationToken)
                        .ConfigureAwait(false),
                    CancellationToken.None);
                var timeoutTask = Task.Delay(initialFrameReadTimeout, initialFrameReadCancellationTokenSource.Token);
                var completedTask = await Task.WhenAny(frameReadTask, timeoutTask);
                if (!ReferenceEquals(completedTask, frameReadTask))
                {
                    TryCancel(initialFrameReadCancellationTokenSource);
                    ObserveFault(frameReadTask);
                    BeginStreamCleanup(stream);
                    cancellationToken.ThrowIfCancellationRequested();
                    return default;
                }

                TryCancel(initialFrameReadCancellationTokenSource);
                readResult = await frameReadTask;
            }

            if (!readResult.IsSuccess)
            {
                var errorResponse = UnityIpcResponseFactory.CreateMalformedFrameResponse(
                    readResult.ErrorKind,
                    readResult.ErrorMessage);
                await WriteModelSafelyAsync(
                    stream,
                    errorResponse,
                    cancellationToken,
                    responseFrameWriteTimeout);

                return default;
            }

            var request = readResult.Value;
            var shutdownAdmissionFinalized = false;
            using var requestCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (CanMonitorPeerDisconnect(stream))
            {
                ObserveFault(MonitorPeerDisconnectAsync(stream, requestCancellationTokenSource));
            }

            try
            {
                if (IsStreamingResponse(request))
                {
                    var streamWriter = new IpcStreamFrameWriter(
                        stream,
                        request,
                        requestCancellationTokenSource.Token,
                        ResolveTransportIoCancellationToken(stream, requestCancellationTokenSource.Token),
                        responseFrameWriteTimeout,
                        _ => TryCancel(requestCancellationTokenSource));
                    var streamingResponse = await ProcessStreamingSafelyAsync(
                        request,
                        streamWriter,
                        requestCancellationTokenSource,
                        cancellationToken);
                    var terminalWritten = await WriteTerminalSafelyAsync(
                        streamWriter,
                        streamingResponse,
                        requestCancellationTokenSource.Token);
                    shutdownAdmissionFinalized = true;
                    AbortShutdownAdmission(request);
                    if (!terminalWritten)
                    {
                        return default;
                    }

                    return new UnityIpcConnectionHandleResult(
                        request,
                        streamingResponse,
                        isShutdownAdmissionCommitted: false);
                }

                var response = await requestHandler.HandleAsync(
                    request,
                    requestCancellationTokenSource.Token);
                var responseWritten = await WriteModelSafelyAsync(
                    stream,
                    response,
                    requestCancellationTokenSource.Token,
                    responseFrameWriteTimeout);
                if (!responseWritten)
                {
                    return default;
                }

                var isShutdownAdmissionCommitted = FinalizeShutdownAdmissionAfterResponseWrite(
                    request,
                    response);
                shutdownAdmissionFinalized = true;

                return new UnityIpcConnectionHandleResult(
                    request,
                    response,
                    isShutdownAdmissionCommitted);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested
                && requestCancellationTokenSource.IsCancellationRequested)
            {
                return default;
            }
            finally
            {
                if (!shutdownAdmissionFinalized)
                {
                    AbortShutdownAdmission(request);
                }

                TryCancel(requestCancellationTokenSource);
            }
        }

        private bool FinalizeShutdownAdmissionAfterResponseWrite (
            IpcRequest request,
            IpcResponse response)
        {
            if (!string.Equals(request.Method, IpcMethodNames.Shutdown, StringComparison.Ordinal))
            {
                return false;
            }

            if (!UnityIpcShutdownResponsePolicy.IsAccepted(request, response))
            {
                AbortShutdownAdmission(request);
                return false;
            }

            return shutdownAdmissionCoordinator.TryCommit(request);
        }

        private void AbortShutdownAdmission (IpcRequest request)
        {
            if (request != null
                && string.Equals(request.Method, IpcMethodNames.Shutdown, StringComparison.Ordinal))
            {
                shutdownAdmissionCoordinator.Abort(request);
            }
        }

        private static bool CanMonitorPeerDisconnect (Stream stream)
        {
            return stream is PipeStream or NetworkStream;
        }

        private static CancellationToken ResolveTransportIoCancellationToken (
            Stream stream,
            CancellationToken operationCancellationToken)
        {
            // Closing the owned PipeStream is the single transport-I/O cancellation mechanism.
            // Unity's Windows Mono runtime can otherwise dispose the pipe operation's internal
            // cancellation source before its overlapped I/O callback completes during Domain Reload.
            return stream is PipeStream
                ? CancellationToken.None
                : operationCancellationToken;
        }

        private static async Task MonitorPeerDisconnectAsync (
            Stream stream,
            CancellationTokenSource requestCancellationTokenSource)
        {
            var buffer = new byte[1];
            var transportReadCancellationToken = ResolveTransportIoCancellationToken(
                stream,
                requestCancellationTokenSource.Token);
            try
            {
                while (!requestCancellationTokenSource.IsCancellationRequested)
                {
                    var readLength = await stream.ReadAsync(
                        buffer,
                        0,
                        buffer.Length,
                        transportReadCancellationToken);
                    if (readLength == 0)
                    {
                        TryCancel(requestCancellationTokenSource);
                        return;
                    }
                }
            }
            catch (OperationCanceledException) when (requestCancellationTokenSource.IsCancellationRequested)
            {
            }
            catch (Exception exception) when (IsConnectionLocalReadFailure(exception))
            {
                TryCancel(requestCancellationTokenSource);
            }
        }

        private static bool IsConnectionLocalReadFailure (Exception exception)
        {
            return exception is IOException
                or SocketException
                or ObjectDisposedException
                or InvalidOperationException
                or NotSupportedException;
        }

        private static void ObserveFault (Task task)
        {
            _ = task.ContinueWith(
                static completedTask => _ = completedTask.Exception,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
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
                return await requestHandler.HandleStreamingAsync(
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

        private static Task<bool> WriteModelSafelyAsync (
            Stream stream,
            IpcResponse response,
            CancellationToken cancellationToken,
            TimeSpan writeTimeout)
        {
            var transportWriteCancellationToken = ResolveTransportIoCancellationToken(
                stream,
                cancellationToken);
            return WriteResponseFrameSafelyAsync(
                stream,
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return IpcFrameCodec.WriteModelAsync(
                            stream,
                            response,
                            IpcJsonSerializerOptions.Default,
                            cancellationToken: transportWriteCancellationToken)
                        .AsTask();
                },
                cancellationToken,
                writeTimeout);
        }

        private static async Task<bool> WriteTerminalSafelyAsync (
            IIpcStreamFrameWriter streamWriter,
            IpcResponse response,
            CancellationToken cancellationToken)
        {
            try
            {
                await streamWriter.WriteTerminalAsync(response, cancellationToken);
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (IpcConnectionWriteFailureClassifier.IsConnectionLocalWriteFailure(exception))
            {
                return false;
            }
        }

        private static async Task<bool> WriteResponseFrameSafelyAsync (
            Stream stream,
            Func<Task> startWrite,
            CancellationToken cancellationToken,
            TimeSpan writeTimeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var writeDeadlineCancellationTokenSource =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var writeTask = Task.Run(startWrite, CancellationToken.None);
                var timeoutTask = Task.Delay(
                    writeTimeout,
                    writeDeadlineCancellationTokenSource.Token);
                var completedTask = await Task.WhenAny(writeTask, timeoutTask);
                if (!ReferenceEquals(completedTask, writeTask))
                {
                    TryCancel(writeDeadlineCancellationTokenSource);
                    ObserveFault(writeTask);
                    BeginStreamCleanup(stream);
                    cancellationToken.ThrowIfCancellationRequested();
                    return false;
                }

                TryCancel(writeDeadlineCancellationTokenSource);
                await writeTask;
                return true;
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
                return false;
            }
        }

        private static void BeginStreamCleanup (Stream stream)
        {
            ObserveFault(Task.Run(() => TryDisposeStream(stream)));
        }

        private static void TryDisposeStream (Stream stream)
        {
            try
            {
                stream.Dispose();
            }
            catch (Exception exception) when (IsConnectionLocalReadFailure(exception))
            {
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
