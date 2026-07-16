using System;
using System.IO;
using System.IO.Pipes;
using System.Net.Sockets;
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
        internal static readonly TimeSpan DefaultInitialFrameReadTimeout = TimeSpan.FromSeconds(5);

        internal static readonly TimeSpan DefaultResponseFrameWriteTimeout = TimeSpan.FromSeconds(1);

        private readonly IUnityIpcRequestHandler requestHandler;

        private readonly IUnityShutdownAdmissionCoordinator shutdownAdmissionCoordinator;

        private readonly IIpcRequestPhaseScopeFactory phaseScopeFactory;

        private readonly bool recoverableReplayAvailable;

        private readonly TimeSpan initialFrameReadTimeout;

        private readonly TimeSpan responseFrameWriteTimeout;

        /// <summary> Initializes one connection handler with explicit dependencies and exchange limits. </summary>
        /// <param name="requestHandler"> The shared IPC request-handler dependency. </param>
        /// <param name="shutdownAdmissionCoordinator"> The shutdown exchange admission coordinator. </param>
        /// <param name="phaseScopeFactory"> The factory that converts request deadlines into exchange-owned monotonic phase scopes. </param>
        /// <param name="recoverableReplayAvailable"> Whether this host has a durable recoverable-operation store available for replay. </param>
        /// <param name="initialFrameReadTimeout"> The upper bound for receiving the first request frame. </param>
        /// <param name="responseFrameWriteTimeout"> The upper bound for writing any response frame. </param>
        /// <exception cref="ArgumentNullException"> Thrown when a dependency is <see langword="null" />. </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when an exchange limit is not positive or exceeds the supported timer duration.
        /// </exception>
        public UnityIpcConnectionHandler (
            IUnityIpcRequestHandler requestHandler,
            IUnityShutdownAdmissionCoordinator shutdownAdmissionCoordinator,
            IIpcRequestPhaseScopeFactory phaseScopeFactory,
            bool recoverableReplayAvailable,
            TimeSpan initialFrameReadTimeout,
            TimeSpan responseFrameWriteTimeout)
        {
            this.requestHandler = requestHandler ?? throw new ArgumentNullException(nameof(requestHandler));
            this.shutdownAdmissionCoordinator = shutdownAdmissionCoordinator ?? throw new ArgumentNullException(nameof(shutdownAdmissionCoordinator));
            this.phaseScopeFactory = phaseScopeFactory ?? throw new ArgumentNullException(nameof(phaseScopeFactory));
            this.recoverableReplayAvailable = recoverableReplayAvailable;
            if (initialFrameReadTimeout <= TimeSpan.Zero
                || initialFrameReadTimeout > IpcRequestPhasePlan.MaximumTimerDuration)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(initialFrameReadTimeout),
                    initialFrameReadTimeout,
                    $"Initial frame read timeout must be greater than zero and at most {IpcRequestPhasePlan.MaximumTimerDuration.TotalMilliseconds:0} milliseconds.");
            }

            this.initialFrameReadTimeout = initialFrameReadTimeout;
            if (responseFrameWriteTimeout <= TimeSpan.Zero
                || responseFrameWriteTimeout > IpcRequestPhasePlan.MaximumTimerDuration)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(responseFrameWriteTimeout),
                    responseFrameWriteTimeout,
                    $"Response frame write timeout must be greater than zero and at most {IpcRequestPhasePlan.MaximumTimerDuration.TotalMilliseconds:0} milliseconds.");
            }

            this.responseFrameWriteTimeout = responseFrameWriteTimeout;
        }

        /// <summary> Handles one request-response exchange over a connected transport stream. </summary>
        /// <param name="stream"> The connected transport stream. </param>
        /// <param name="cancellationToken"> The host listener-generation cancellation token. Peer connection lifetime is tracked separately after the request frame is read. </param>
        /// <returns> The handled connection exchange result. </returns>
        /// <exception cref="OperationCanceledException"> Thrown when operation is canceled. </exception>
        public async Task<UnityIpcConnectionHandleResult> HandleAsync (
            Stream stream,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IpcFrameReadResult<IpcRequestEnvelope> readResult;
            using (var initialFrameReadCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                var transportReadCancellationToken = ResolveTransportIoCancellationToken(
                    stream,
                    initialFrameReadCancellationTokenSource.Token);
                var frameReadTask = Task.Run(
                    async () => await IpcFrameCodec.TryReadModelAsync<IpcRequestEnvelope>(
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
                    return UnityIpcConnectionHandleResult.NoTerminalResponse;
                }

                TryCancel(initialFrameReadCancellationTokenSource);
                readResult = await frameReadTask;
            }

            if (!readResult.IsSuccess)
            {
                var errorResponse = UnityIpcResponseFactory.CreateMalformedFrameResponse(
                    readResult.ErrorKind,
                    readResult.ErrorMessage);
                await WriteModelWithRelativeTimeoutSafelyAsync(
                    stream,
                    errorResponse,
                    cancellationToken,
                    responseFrameWriteTimeout);

                return UnityIpcConnectionHandleResult.NoTerminalResponse;
            }

            var requestEnvelope = readResult.Value;
            var shutdownAdmissionFinalized = false;
            ValidatedUnityIpcRequest validatedRequest = null;
            using var connectionLifetimeCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (CanMonitorPeerDisconnect(stream))
            {
                ObserveFault(MonitorPeerDisconnectAsync(stream, connectionLifetimeCancellationTokenSource));
            }

            // Authorization is bounded by the request deadline and host generation. After the
            // endpoint method is validated, non-recoverable execution also observes peer lifetime.
            var phaseScope = phaseScopeFactory.Create(
                requestEnvelope,
                cancellationToken,
                responseFrameWriteTimeout);

            using (phaseScope)
            {
                try
                {
                    var validationResult = await requestHandler.ValidateAsync(
                        requestEnvelope,
                        phaseScope);
                    if (!validationResult.IsSuccess)
                    {
                        var validationErrorResponse = validationResult.ErrorResponse;
                        bool validationErrorWritten;
                        if (validationResult.ResponseMode == IpcResponseMode.Stream)
                        {
                            using var validationErrorWriter = new IpcStreamFrameWriter(
                                stream,
                                requestEnvelope,
                                connectionLifetimeCancellationTokenSource.Token,
                                ResolveTransportIoCancellationToken(stream, connectionLifetimeCancellationTokenSource.Token),
                                phaseScope.WriteCutoffToken,
                                _ => TryCancel(connectionLifetimeCancellationTokenSource));
                            validationErrorWritten = await WriteTerminalSafelyAsync(
                                validationErrorWriter,
                                validationErrorResponse,
                                connectionLifetimeCancellationTokenSource.Token);
                        }
                        else
                        {
                            validationErrorWritten = await WriteModelSafelyAsync(
                                stream,
                                validationErrorResponse,
                                connectionLifetimeCancellationTokenSource.Token,
                                phaseScope.WriteCutoffToken);
                        }

                        if (!validationErrorWritten)
                        {
                            return UnityIpcConnectionHandleResult.NoTerminalResponse;
                        }

                        shutdownAdmissionFinalized = true;
                        return UnityIpcConnectionHandleResult.ValidationFailure(validationErrorResponse);
                    }

                    validatedRequest = validationResult.Request;
                    if (!recoverableReplayAvailable
                        || !UnityIpcMethodCapabilities.SupportsRecoverableReplay(validatedRequest.Method))
                    {
                        phaseScope.AttachExecutionUpstream(connectionLifetimeCancellationTokenSource.Token);
                    }

                    if (validatedRequest.ResponseMode == IpcResponseMode.Stream)
                    {
                        using var streamWriter = new IpcStreamFrameWriter(
                            stream,
                            validatedRequest,
                            connectionLifetimeCancellationTokenSource.Token,
                            ResolveTransportIoCancellationToken(stream, connectionLifetimeCancellationTokenSource.Token),
                            phaseScope.WriteCutoffToken,
                            _ => TryCancel(connectionLifetimeCancellationTokenSource));
                        var streamingResponse = await ProcessStreamingSafelyAsync(
                            validatedRequest,
                            streamWriter,
                            phaseScope,
                            connectionLifetimeCancellationTokenSource,
                            cancellationToken);
                        var terminalWritten = await WriteTerminalSafelyAsync(
                            streamWriter,
                            streamingResponse,
                            connectionLifetimeCancellationTokenSource.Token);
                        shutdownAdmissionFinalized = true;
                        AbortShutdownAdmission(validatedRequest);
                        if (!terminalWritten)
                        {
                            return UnityIpcConnectionHandleResult.NoTerminalResponse;
                        }

                        return new UnityIpcConnectionHandleResult(
                            validatedRequest,
                            streamingResponse,
                            isShutdownAdmissionCommitted: false);
                    }

                    var response = await requestHandler.HandleAsync(
                        validatedRequest,
                        phaseScope);
                    var responseWritten = await WriteModelSafelyAsync(
                        stream,
                        response,
                        connectionLifetimeCancellationTokenSource.Token,
                        phaseScope.WriteCutoffToken);
                    if (!responseWritten)
                    {
                        return UnityIpcConnectionHandleResult.NoTerminalResponse;
                    }

                    var isShutdownAdmissionCommitted = FinalizeShutdownAdmissionAfterResponseWrite(
                        validatedRequest,
                        response);
                    shutdownAdmissionFinalized = true;

                    return new UnityIpcConnectionHandleResult(
                        validatedRequest,
                        response,
                        isShutdownAdmissionCommitted);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested
                    && connectionLifetimeCancellationTokenSource.IsCancellationRequested)
                {
                    return UnityIpcConnectionHandleResult.NoTerminalResponse;
                }
                finally
                {
                    if (!shutdownAdmissionFinalized
                        && validatedRequest != null)
                    {
                        AbortShutdownAdmission(validatedRequest);
                    }

                    TryCancel(connectionLifetimeCancellationTokenSource);
                }
            }
        }

        private bool FinalizeShutdownAdmissionAfterResponseWrite (
            ValidatedUnityIpcRequest request,
            IpcResponse response)
        {
            if (request.Method != UnityIpcMethod.Shutdown)
            {
                return false;
            }

            if (!UnityIpcShutdownResponsePolicy.IsAccepted(request.Method, response))
            {
                AbortShutdownAdmission(request);
                return false;
            }

            return shutdownAdmissionCoordinator.TryCommit(request);
        }

        private void AbortShutdownAdmission (ValidatedUnityIpcRequest request)
        {
            if (request.Method == UnityIpcMethod.Shutdown)
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
            CancellationTokenSource connectionLifetimeCancellationTokenSource)
        {
            var buffer = new byte[1];
            var transportReadCancellationToken = ResolveTransportIoCancellationToken(
                stream,
                connectionLifetimeCancellationTokenSource.Token);
            try
            {
                while (!connectionLifetimeCancellationTokenSource.IsCancellationRequested)
                {
                    var readLength = await stream.ReadAsync(
                        buffer,
                        0,
                        buffer.Length,
                        transportReadCancellationToken);
                    if (readLength == 0)
                    {
                        TryCancel(connectionLifetimeCancellationTokenSource);
                        return;
                    }
                }
            }
            catch (OperationCanceledException) when (connectionLifetimeCancellationTokenSource.IsCancellationRequested)
            {
            }
            catch (Exception exception) when (IsConnectionLocalReadFailure(exception))
            {
                TryCancel(connectionLifetimeCancellationTokenSource);
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

        private async Task<IpcResponse> ProcessStreamingSafelyAsync (
            ValidatedUnityIpcRequest request,
            IIpcStreamFrameWriter streamWriter,
            IpcRequestPhaseScope phaseScope,
            CancellationTokenSource connectionLifetimeCancellationTokenSource,
            CancellationToken connectionCancellationToken)
        {
            try
            {
                return await requestHandler.HandleStreamingAsync(
                    request,
                    streamWriter,
                    phaseScope);
            }
            catch (OperationCanceledException) when (!connectionCancellationToken.IsCancellationRequested
                && connectionLifetimeCancellationTokenSource.IsCancellationRequested)
            {
                return CreateStreamWriteFailureResponse(request, "Streaming IPC request was canceled because the response stream failed.");
            }
            catch (Exception exception) when (connectionLifetimeCancellationTokenSource.IsCancellationRequested
                && IpcConnectionWriteFailureClassifier.IsConnectionLocalWriteFailure(exception))
            {
                return CreateStreamWriteFailureResponse(request, $"Streaming IPC response stream failed. {exception.Message}");
            }
        }

        private static IpcResponse CreateStreamWriteFailureResponse (
            ValidatedUnityIpcRequest request,
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
            CancellationToken connectionLifetimeCancellationToken,
            CancellationToken writeCutoffToken)
        {
            var transportWriteCancellationToken = ResolveTransportIoCancellationToken(
                stream,
                connectionLifetimeCancellationToken);
            return WriteResponseFrameSafelyAsync(
                stream,
                () =>
                {
                    connectionLifetimeCancellationToken.ThrowIfCancellationRequested();
                    return IpcFrameCodec.WriteModelAsync(
                            stream,
                            response,
                            IpcJsonSerializerOptions.Default,
                            cancellationToken: transportWriteCancellationToken)
                        .AsTask();
                },
                connectionLifetimeCancellationToken,
                writeCutoffToken);
        }

        private static async Task<bool> WriteModelWithRelativeTimeoutSafelyAsync (
            Stream stream,
            IpcResponse response,
            CancellationToken connectionLifetimeCancellationToken,
            TimeSpan writeTimeout)
        {
            using var writeCutoffCancellationTokenSource =
                new CancellationTokenSource(writeTimeout);
            return await WriteModelSafelyAsync(
                stream,
                response,
                connectionLifetimeCancellationToken,
                writeCutoffCancellationTokenSource.Token);
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
            CancellationToken connectionLifetimeCancellationToken,
            CancellationToken writeCutoffToken)
        {
            connectionLifetimeCancellationToken.ThrowIfCancellationRequested();
            if (writeCutoffToken.IsCancellationRequested)
            {
                return false;
            }

            try
            {
                var writeTask = Task.Run(startWrite, CancellationToken.None);
                var connectionCancellationTask = Task.Delay(
                    Timeout.Infinite,
                    connectionLifetimeCancellationToken);
                var writeCutoffTask = Task.Delay(
                    Timeout.Infinite,
                    writeCutoffToken);
                var completedTask = await Task.WhenAny(
                    writeTask,
                    connectionCancellationTask,
                    writeCutoffTask);
                if (!ReferenceEquals(completedTask, writeTask))
                {
                    ObserveFault(writeTask);
                    BeginStreamCleanup(stream);
                    if (ReferenceEquals(completedTask, connectionCancellationTask)
                        || connectionLifetimeCancellationToken.IsCancellationRequested)
                    {
                        connectionLifetimeCancellationToken.ThrowIfCancellationRequested();
                    }

                    return false;
                }

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
