using System.Net.Sockets;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Ipc;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Transport;

/// <summary> Implements transport-level IPC communication with explicitly resolved endpoints. </summary>
internal sealed class IpcTransportClient : IIpcTransportClient
{
    /// <summary> Gets the maximum time spent on one connection attempt before any request bytes are written. </summary>
    internal static TimeSpan ConnectionAttemptTimeoutCap { get; } = TimeSpan.FromSeconds(1);

    private readonly IIpcTransportConnector connector;

    private readonly TimeProvider timeProvider;

    private int activeStreamingOperation;

    /// <summary> Initializes the transport client with its connection and clock dependencies. </summary>
    /// <param name="connector"> The IPC stream connector. </param>
    /// <param name="timeProvider"> The clock used for outward transport deadlines. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public IpcTransportClient (
        IIpcTransportConnector connector,
        TimeProvider timeProvider)
    {
        this.connector = connector ?? throw new ArgumentNullException(nameof(connector));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc />
    public ValueTask<IpcResponse> SendAsync (
        IpcEndpoint endpoint,
        IpcRequestEnvelope request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureResponseMode(request, IpcResponseMode.Single, nameof(SendAsync));

        return SendCoreAsync(
            endpoint,
            request,
            timeout,
            responseWaitIsBounded: true,
            onProgressFrame: null,
            cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IpcResponse> SendStreamingAsync (
        IpcEndpoint endpoint,
        IpcRequestEnvelope request,
        TimeSpan timeout,
        Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(onProgressFrame);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureResponseMode(request, IpcResponseMode.Stream, nameof(SendStreamingAsync));

        return SendCoreAsync(
            endpoint,
            request,
            timeout,
            responseWaitIsBounded: true,
            onProgressFrame,
            cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IpcResponse> SendWithUnboundedResponseWaitAsync (
        IpcEndpoint endpoint,
        IpcRequestEnvelope request,
        TimeSpan sendTimeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sendTimeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureResponseMode(request, IpcResponseMode.Single, nameof(SendWithUnboundedResponseWaitAsync));

        return SendCoreAsync(
            endpoint,
            request,
            sendTimeout,
            responseWaitIsBounded: false,
            onProgressFrame: null,
            cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IpcResponse> SendStreamingWithUnboundedResponseWaitAsync (
        IpcEndpoint endpoint,
        IpcRequestEnvelope request,
        TimeSpan sendTimeout,
        Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(onProgressFrame);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sendTimeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureResponseMode(request, IpcResponseMode.Stream, nameof(SendStreamingWithUnboundedResponseWaitAsync));

        return SendCoreAsync(
            endpoint,
            request,
            sendTimeout,
            responseWaitIsBounded: false,
            onProgressFrame,
            cancellationToken);
    }

    private async ValueTask<IpcResponse> SendCoreAsync (
        IpcEndpoint endpoint,
        IpcRequestEnvelope request,
        TimeSpan timeout,
        bool responseWaitIsBounded,
        Func<IpcStreamFrame, CancellationToken, ValueTask>? onProgressFrame,
        CancellationToken cancellationToken)
    {
        var holdsStreamingAdmission = onProgressFrame is not null;
        if (holdsStreamingAdmission
            && Interlocked.CompareExchange(ref activeStreamingOperation, 1, 0) != 0)
        {
            throw new IpcStreamingOperationInProgressException();
        }

        var operationOwnsStreamingAdmission = false;
        try
        {
            var raceSignals = new TransportRaceSignals(responseWaitIsBounded);
            using var callerCancellationRegistration = cancellationToken.CanBeCanceled
                ? cancellationToken.UnsafeRegister(
                    static state => ((TransportRaceSignals)state!).SignalCallerCancellation(),
                    raceSignals)
                : default;
            using var deadlineDelayCancellationTokenSource = new CancellationTokenSource();
            var deadlineTask = Task.Delay(
                timeout,
                timeProvider,
                deadlineDelayCancellationTokenSource.Token);
            _ = deadlineTask.ContinueWith(
                static (completedTask, state) =>
                {
                    if (completedTask.Status == TaskStatus.RanToCompletion)
                    {
                        ((TransportRaceSignals)state!).SignalOverallDeadline();
                    }
                },
                raceSignals,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            using var connectionDeadlineCancellationTokenSource = new CancellationTokenSource();
            var connectionTimeout = GetShorterTimeout(timeout, ConnectionAttemptTimeoutCap);
            var connectionDeadlineTask = Task.Delay(
                connectionTimeout,
                timeProvider,
                connectionDeadlineCancellationTokenSource.Token);
            _ = connectionDeadlineTask.ContinueWith(
                static (completedTask, state) =>
                {
                    if (completedTask.Status == TaskStatus.RanToCompletion)
                    {
                        ((TransportRaceSignals)state!).SignalConnectionDeadline();
                    }
                },
                raceSignals,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            var operationCancellationTokenSource = new CancellationTokenSource();
            var disposeOperationCancellationTokenSource = true;
            var operationState = new IpcTransportOperationState(raceSignals);
            var operationTask = Task.Run(
                async () =>
                {
                    try
                    {
                        return await ExecuteTransportOperationAsync(
                                endpoint,
                                request,
                                onProgressFrame,
                                operationState,
                                raceSignals,
                                operationCancellationTokenSource.Token)
                            .ConfigureAwait(false);
                    }
                    finally
                    {
                        raceSignals.SignalOperationCompleted();
                        if (holdsStreamingAdmission)
                        {
                            Volatile.Write(ref activeStreamingOperation, 0);
                        }
                    }
                },
                CancellationToken.None);
            operationOwnsStreamingAdmission = holdsStreamingAdmission;

            void AbandonOperation ()
            {
                AbandonTransportOperation(
                    operationTask,
                    operationCancellationTokenSource,
                    operationState);
                disposeOperationCancellationTokenSource = false;
            }

            try
            {
                var connectionPhaseCompletion = await raceSignals.ConnectionPhaseCompletion
                    .ConfigureAwait(false);

                if (connectionPhaseCompletion == ConnectionPhaseCompletion.CallerCancellation)
                {
                    AbandonOperation();
                    throw new OperationCanceledException(cancellationToken);
                }

                if (connectionPhaseCompletion is ConnectionPhaseCompletion.ConnectionDeadline
                    or ConnectionPhaseCompletion.OverallDeadline)
                {
                    AbandonOperation();
                    throw CreateConnectTimeoutException(connectionTimeout);
                }

                connectionDeadlineCancellationTokenSource.Cancel();

                if (connectionPhaseCompletion == ConnectionPhaseCompletion.Operation)
                {
                    return await operationTask.ConfigureAwait(false);
                }

                if (connectionPhaseCompletion != ConnectionPhaseCompletion.ConnectionEstablished)
                {
                    throw new InvalidOperationException($"Unsupported IPC connection phase completion: {connectionPhaseCompletion}.");
                }

                var sendPhaseCompletion = await raceSignals.SendPhaseCompletion.ConfigureAwait(false);
                if (sendPhaseCompletion == SendPhaseCompletion.Operation)
                {
                    return await operationTask.ConfigureAwait(false);
                }

                if (sendPhaseCompletion == SendPhaseCompletion.CallerCancellation)
                {
                    AbandonOperation();
                    throw new OperationCanceledException(cancellationToken);
                }

                if (sendPhaseCompletion == SendPhaseCompletion.Deadline)
                {
                    AbandonOperation();
                    throw CreateTransportTimeoutException(
                        responseWaitIsBounded,
                        onProgressFrame is not null,
                        timeout);
                }

                if (sendPhaseCompletion != SendPhaseCompletion.RequestWritten)
                {
                    throw new InvalidOperationException($"Unsupported IPC send phase completion: {sendPhaseCompletion}.");
                }

                deadlineDelayCancellationTokenSource.Cancel();
                var unboundedResponseCompletion = await raceSignals.UnboundedResponseCompletion.ConfigureAwait(false);
                if (unboundedResponseCompletion == UnboundedResponseCompletion.CallerCancellation)
                {
                    AbandonOperation();
                    throw new OperationCanceledException(cancellationToken);
                }

                if (unboundedResponseCompletion != UnboundedResponseCompletion.Operation)
                {
                    throw new InvalidOperationException($"Unsupported IPC response phase completion: {unboundedResponseCompletion}.");
                }

                return await operationTask.ConfigureAwait(false);
            }
            finally
            {
                connectionDeadlineCancellationTokenSource.Cancel();
                deadlineDelayCancellationTokenSource.Cancel();
                if (disposeOperationCancellationTokenSource)
                {
                    operationCancellationTokenSource.Dispose();
                }
            }
        }
        finally
        {
            if (holdsStreamingAdmission && !operationOwnsStreamingAdmission)
            {
                Volatile.Write(ref activeStreamingOperation, 0);
            }
        }
    }

    private async ValueTask<IpcResponse> ExecuteTransportOperationAsync (
        IpcEndpoint endpoint,
        IpcRequestEnvelope request,
        Func<IpcStreamFrame, CancellationToken, ValueTask>? onProgressFrame,
        IpcTransportOperationState operationState,
        TransportRaceSignals raceSignals,
        CancellationToken cancellationToken)
    {
        Stream? stream = null;
        try
        {
            stream = await ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
            if (!operationState.TryAttach(stream))
            {
                ScheduleStreamCleanup(stream);
                stream = null;
                throw new OperationCanceledException(cancellationToken);
            }

            await IpcFrameCodec.WriteModelAsync(
                    stream,
                    request,
                    IpcJsonSerializerOptions.Default,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            raceSignals.SignalRequestWritten();

            if (onProgressFrame is null)
            {
                var response = await ReadResponseModelAsync<IpcResponse>(stream, cancellationToken).ConfigureAwait(false);
                ValidateIpcResponse(request, response);
                return response;
            }

            return await ReadStreamingResponseAsync(
                    stream,
                    request,
                    onProgressFrame,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            if (stream is not null && operationState.TryTakeForCleanup(stream))
            {
                ScheduleStreamCleanup(stream);
            }
        }
    }

    private async ValueTask<Stream> ConnectAsync (
        IpcEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        try
        {
            return await connector.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException exception)
        {
            throw new IpcConnectTimeoutException(
                "IPC connection failed with a transport timeout before the request was sent.",
                exception);
        }
        catch (Exception exception) when (exception is SocketException or IOException)
        {
            throw new IpcConnectException(
                "IPC connection failed before the request was sent.",
                exception);
        }
    }

    private static TimeoutException CreateTransportTimeoutException (
        bool responseWaitIsBounded,
        bool isStreaming,
        TimeSpan timeout)
    {
        var requestKind = isStreaming
            ? "IPC streaming request"
            : "IPC request";
        var phase = responseWaitIsBounded
            ? string.Empty
            : " write";
        return new TimeoutException(
            $"{requestKind}{phase} timed out after {timeout.TotalMilliseconds:0} milliseconds.");
    }

    private static IpcConnectTimeoutException CreateConnectTimeoutException (TimeSpan connectionTimeout)
    {
        return new IpcConnectTimeoutException(
            $"IPC connection timed out after {connectionTimeout.TotalMilliseconds:0} milliseconds before the request was sent.");
    }

    private static TimeSpan GetShorterTimeout (
        TimeSpan first,
        TimeSpan second)
    {
        return first < second ? first : second;
    }

    private static void ScheduleStreamCleanup (Stream stream)
    {
        ObserveFault(Task.Run(
            async () => await stream.DisposeAsync().ConfigureAwait(false),
            CancellationToken.None));
    }

    private static void AbandonTransportOperation (
        Task operationTask,
        CancellationTokenSource operationCancellationTokenSource,
        IpcTransportOperationState operationState)
    {
        var abortTask = operationState.AbortAsync();
        var cancellationRequestTask = operationCancellationTokenSource.CancelAsync();
        ObserveFault(abortTask);
        ObserveAndDisposeAfterCompletion(
            operationTask,
            cancellationRequestTask,
            operationCancellationTokenSource);
    }

    private static void EnsureResponseMode (
        IpcRequestEnvelope request,
        IpcResponseMode expectedResponseMode,
        string operationName)
    {
        if (ContractLiteralCodec.TryParse<IpcResponseMode>(request.ResponseMode, out var responseMode)
            && responseMode == expectedResponseMode)
        {
            return;
        }

        var expectedLiteral = ContractLiteralCodec.ToValue(expectedResponseMode);
        throw new InvalidOperationException($"IPC {operationName} requires responseMode='{expectedLiteral}'. Actual: {request.ResponseMode}.");
    }

    private static async ValueTask<IpcResponse> ReadStreamingResponseAsync (
        Stream stream,
        IpcRequestEnvelope request,
        Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var frame = await ReadResponseModelAsync<IpcStreamFrame>(stream, cancellationToken).ConfigureAwait(false);

            ValidateStreamingFrame(request, frame);
            if (frame.Kind == IpcStreamFrameKind.Progress)
            {
                try
                {
                    await onProgressFrame(frame, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    throw new IpcProgressFrameHandlerException(exception);
                }

                continue;
            }

            return frame.Response!;
        }
    }

    private static async ValueTask<T> ReadResponseModelAsync<T> (
        Stream stream,
        CancellationToken cancellationToken)
    {
        try
        {
            return await IpcFrameCodec.ReadModelAsync<T>(
                    stream,
                    IpcJsonSerializerOptions.Default,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (IOException exception)
        {
            throw new IpcResponseReadInterruptedException(exception);
        }
    }

    private static void ObserveAndDisposeAfterCompletion (
        Task operationTask,
        Task cancellationRequestTask,
        CancellationTokenSource operationCancellationTokenSource)
    {
        ObserveFault(operationTask);
        ObserveFault(cancellationRequestTask);
        _ = Task.WhenAll(operationTask, cancellationRequestTask).ContinueWith(
            static (completedTask, state) =>
            {
                _ = completedTask.Exception;
                ((CancellationTokenSource)state!).Dispose();
            },
            operationCancellationTokenSource,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static void ObserveFault (Task task)
    {
        _ = task.ContinueWith(
            static completedTask => _ = completedTask.Exception,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    private static void ValidateStreamingFrame (
        IpcRequestEnvelope request,
        IpcStreamFrame frame)
    {
        if (frame.ProtocolVersion != IpcProtocol.CurrentVersion)
        {
            throw new InvalidDataException(
                $"IPC stream frame protocol version mismatch. Requested={IpcProtocol.CurrentVersion}, Actual={frame.ProtocolVersion}.");
        }

        if (frame.RequestId != request.RequestId)
        {
            throw new InvalidDataException(
                $"IPC stream frame requestId mismatch. Expected={request.RequestId}, Actual={frame.RequestId}.");
        }

    }

    private static void ValidateIpcResponse (
        IpcRequestEnvelope request,
        IpcResponse response)
    {
        if (response.ProtocolVersion != IpcProtocol.CurrentVersion)
        {
            throw new InvalidDataException(
                $"IPC response protocol version mismatch. Requested={IpcProtocol.CurrentVersion}, Actual={response.ProtocolVersion}.");
        }

        if (response.RequestId != request.RequestId)
        {
            throw new InvalidDataException(
                $"IPC response requestId mismatch. Expected={request.RequestId}, Actual={response.RequestId}.");
        }

    }

    private enum ConnectionPhaseCompletion
    {
        ConnectionEstablished,
        Operation,
        ConnectionDeadline,
        OverallDeadline,
        CallerCancellation,
    }

    private enum SendPhaseCompletion
    {
        RequestWritten,
        Operation,
        Deadline,
        CallerCancellation,
    }

    private enum UnboundedResponseCompletion
    {
        Operation,
        CallerCancellation,
    }

    private sealed class TransportRaceSignals
    {
        private readonly bool responseWaitIsBounded;

        private readonly TaskCompletionSource<ConnectionPhaseCompletion> connectionPhaseCompletionSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<SendPhaseCompletion> sendPhaseCompletionSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<UnboundedResponseCompletion> unboundedResponseCompletionSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TransportRaceSignals (bool responseWaitIsBounded)
        {
            this.responseWaitIsBounded = responseWaitIsBounded;
        }

        public Task<ConnectionPhaseCompletion> ConnectionPhaseCompletion => connectionPhaseCompletionSource.Task;

        public Task<SendPhaseCompletion> SendPhaseCompletion => sendPhaseCompletionSource.Task;

        public Task<UnboundedResponseCompletion> UnboundedResponseCompletion => unboundedResponseCompletionSource.Task;

        public void SignalConnectionEstablished ()
        {
            connectionPhaseCompletionSource.TrySetResult(IpcTransportClient.ConnectionPhaseCompletion.ConnectionEstablished);
        }

        public void SignalRequestWritten ()
        {
            if (!responseWaitIsBounded)
            {
                sendPhaseCompletionSource.TrySetResult(IpcTransportClient.SendPhaseCompletion.RequestWritten);
            }
        }

        public void SignalOperationCompleted ()
        {
            connectionPhaseCompletionSource.TrySetResult(IpcTransportClient.ConnectionPhaseCompletion.Operation);
            sendPhaseCompletionSource.TrySetResult(IpcTransportClient.SendPhaseCompletion.Operation);
            unboundedResponseCompletionSource.TrySetResult(IpcTransportClient.UnboundedResponseCompletion.Operation);
        }

        public void SignalConnectionDeadline ()
        {
            connectionPhaseCompletionSource.TrySetResult(IpcTransportClient.ConnectionPhaseCompletion.ConnectionDeadline);
        }

        public void SignalOverallDeadline ()
        {
            connectionPhaseCompletionSource.TrySetResult(IpcTransportClient.ConnectionPhaseCompletion.OverallDeadline);
            sendPhaseCompletionSource.TrySetResult(IpcTransportClient.SendPhaseCompletion.Deadline);
        }

        public void SignalCallerCancellation ()
        {
            connectionPhaseCompletionSource.TrySetResult(IpcTransportClient.ConnectionPhaseCompletion.CallerCancellation);
            sendPhaseCompletionSource.TrySetResult(IpcTransportClient.SendPhaseCompletion.CallerCancellation);
            unboundedResponseCompletionSource.TrySetResult(IpcTransportClient.UnboundedResponseCompletion.CallerCancellation);
        }
    }

    private sealed class IpcTransportOperationState
    {
        private readonly object syncRoot = new object();

        private readonly TransportRaceSignals raceSignals;

        private Stream? stream;

        private bool abortRequested;

        public IpcTransportOperationState (TransportRaceSignals raceSignals)
        {
            this.raceSignals = raceSignals ?? throw new ArgumentNullException(nameof(raceSignals));
        }

        public bool TryAttach (Stream connectedStream)
        {
            ArgumentNullException.ThrowIfNull(connectedStream);

            lock (syncRoot)
            {
                if (abortRequested)
                {
                    return false;
                }

                stream = connectedStream;
            }

            raceSignals.SignalConnectionEstablished();
            return true;
        }

        public bool TryTakeForCleanup (Stream connectedStream)
        {
            lock (syncRoot)
            {
                if (!ReferenceEquals(stream, connectedStream))
                {
                    return false;
                }

                stream = null;
                return true;
            }
        }

        public Task AbortAsync ()
        {
            Stream? streamToAbort;
            lock (syncRoot)
            {
                if (abortRequested)
                {
                    return Task.CompletedTask;
                }

                abortRequested = true;
                streamToAbort = stream;
                stream = null;
            }

            return streamToAbort is null
                ? Task.CompletedTask
                : Task.Run(streamToAbort.Dispose, CancellationToken.None);
        }
    }

}
