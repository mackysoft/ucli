using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Infrastructure.Ipc;

/// <summary> Writes length-prefixed IPC stream frames to one connected transport stream. </summary>
internal sealed class IpcStreamFrameWriter : IIpcStreamFrameWriter
{
    private readonly Stream stream;

    private readonly Guid requestId;

    private readonly CancellationToken connectionLifetimeCancellationToken;

    private readonly CancellationToken transportWriteCancellationToken;

    private readonly TimeSpan frameWriteTimeout;

    private readonly Action<Exception>? writeFailureHandler;

    private readonly SemaphoreSlim writeGate = new(1, 1);

    private Exception? terminalWriteFailure;

    /// <summary> Initializes a new instance of the <see cref="IpcStreamFrameWriter" /> class. </summary>
    /// <param name="stream"> The connected transport stream. </param>
    /// <param name="request"> The request that owns the stream response. </param>
    /// <param name="connectionLifetimeCancellationToken"> The cancellation token for the connected transport lifetime. </param>
    /// <param name="transportWriteCancellationToken"> The cancellation token passed to transport write operations. </param>
    /// <param name="frameWriteTimeout"> The upper bound for writing one frame. </param>
    /// <param name="writeFailureHandler"> The callback invoked when a connection-local write failure occurs, or <see langword="null" /> when no notification is required. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="stream" /> or <paramref name="request" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="frameWriteTimeout" /> is not positive. </exception>
    public IpcStreamFrameWriter (
        Stream stream,
        IpcRequest request,
        CancellationToken connectionLifetimeCancellationToken,
        CancellationToken transportWriteCancellationToken,
        TimeSpan frameWriteTimeout,
        Action<Exception>? writeFailureHandler)
    {
        this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        requestId = request.RequestId;
        this.connectionLifetimeCancellationToken = connectionLifetimeCancellationToken;
        this.transportWriteCancellationToken = transportWriteCancellationToken;
        if (frameWriteTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(frameWriteTimeout),
                frameWriteTimeout,
                "Frame write timeout must be greater than zero.");
        }

        this.frameWriteTimeout = frameWriteTimeout;
        this.writeFailureHandler = writeFailureHandler;
    }

    /// <summary> Writes one progress frame. </summary>
    /// <typeparam name="TPayload"> The progress payload type. </typeparam>
    /// <param name="eventName"> The progress event name. </param>
    /// <param name="payload"> The progress payload. </param>
    /// <param name="cancellationToken"> The cancellation token that can stop the frame before its transport write starts. </param>
    /// <returns> A task that completes after the frame is flushed. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="eventName" /> is empty. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="payload" /> is <see langword="null" />. </exception>
    public async ValueTask WriteProgressAsync<TPayload> (
        string eventName,
        TPayload payload,
        CancellationToken cancellationToken = default)
        where TPayload : notnull
    {
        if (string.IsNullOrWhiteSpace(eventName))
        {
            throw new ArgumentException("Progress event name must not be empty.", nameof(eventName));
        }

        if (payload == null)
        {
            throw new ArgumentNullException(nameof(payload));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var frame = new IpcStreamFrame(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: requestId,
            kind: IpcStreamFrameKinds.Progress,
            @event: eventName,
            payload: IpcPayloadCodec.SerializeToElement(payload),
            response: null);
        await WriteFrameAsync(frame, cancellationToken).ConfigureAwait(false);
    }

    /// <summary> Writes the terminal response frame. </summary>
    /// <param name="response"> The terminal response. </param>
    /// <param name="cancellationToken"> The cancellation token that can stop the frame before its transport write starts. </param>
    /// <returns> A task that completes after the frame is flushed. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="response" /> is <see langword="null" />. </exception>
    public async ValueTask WriteTerminalAsync (
        IpcResponse response,
        CancellationToken cancellationToken = default)
    {
        if (response == null)
        {
            throw new ArgumentNullException(nameof(response));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var frame = new IpcStreamFrame(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: requestId,
            kind: IpcStreamFrameKinds.Terminal,
            @event: null,
            payload: IpcPayloadCodec.SerializeToElement(new UcliEmptyArgs()),
            response: response);
        await WriteFrameAsync(frame, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask WriteFrameAsync (
        IpcStreamFrame frame,
        CancellationToken frameAdmissionCancellationToken)
    {
        ThrowIfWriterUnavailable();
        await writeGate.WaitAsync(frameAdmissionCancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfWriterUnavailable();
            try
            {
                // NOTE: Once admitted, a length-prefixed frame must not be interrupted by an execution deadline.
                // Only the connection lifetime and the independent frame deadline may stop its transport write.
                connectionLifetimeCancellationToken.ThrowIfCancellationRequested();
                using var frameWriteTimeoutCancellationTokenSource =
                    CancellationTokenSource.CreateLinkedTokenSource(connectionLifetimeCancellationToken);
                var timeoutTask = Task.Delay(
                    frameWriteTimeout,
                    frameWriteTimeoutCancellationTokenSource.Token);
                var writeTask = Task.Run(
                    async () =>
                    {
                        connectionLifetimeCancellationToken.ThrowIfCancellationRequested();
                        await IpcFrameCodec.WriteModelAsync(
                                stream,
                                frame,
                                IpcJsonSerializerOptions.Default,
                                cancellationToken: transportWriteCancellationToken)
                            .ConfigureAwait(false);
                    },
                    CancellationToken.None);
                var completedTask = await Task.WhenAny(writeTask, timeoutTask).ConfigureAwait(false);
                if (!ReferenceEquals(completedTask, writeTask))
                {
                    ObserveFault(writeTask);
                    if (connectionLifetimeCancellationToken.IsCancellationRequested)
                    {
                        connectionLifetimeCancellationToken.ThrowIfCancellationRequested();
                    }

                    var timeoutException = new IOException(
                        $"Timed out while writing an IPC stream frame after {frameWriteTimeout.TotalMilliseconds:0} milliseconds.");
                    var recordedFailure = RecordTerminalWriteFailure(
                        timeoutException,
                        notifyWriteFailure: true);
                    throw recordedFailure;
                }

                frameWriteTimeoutCancellationTokenSource.Cancel();
                await writeTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException exception) when (connectionLifetimeCancellationToken.IsCancellationRequested)
            {
                RecordTerminalWriteFailure(
                    exception,
                    notifyWriteFailure: false);
                throw;
            }
            catch (Exception exception) when (IpcConnectionWriteFailureClassifier.IsConnectionLocalWriteFailure(exception))
            {
                throw RecordTerminalWriteFailure(
                    exception,
                    notifyWriteFailure: true);
            }
        }
        finally
        {
            writeGate.Release();
        }
    }

    private Exception RecordTerminalWriteFailure (
        Exception exception,
        bool notifyWriteFailure)
    {
        var existingFailure = Interlocked.CompareExchange(
            ref terminalWriteFailure,
            exception,
            comparand: null);
        if (existingFailure is not null)
        {
            return existingFailure;
        }

        ObserveFault(Task.Run(() =>
        {
            try
            {
                if (notifyWriteFailure)
                {
                    writeFailureHandler?.Invoke(exception);
                }
            }
            finally
            {
                TryDisposeStream(stream);
            }
        }));

        return exception;
    }

    private void ThrowIfWriterUnavailable ()
    {
        var failure = Volatile.Read(ref terminalWriteFailure);
        if (failure is not null)
        {
            throw failure;
        }
    }

    private static void ObserveFault (Task task)
    {
        _ = task.ContinueWith(
            static completedTask => _ = completedTask.Exception,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    private static void TryDisposeStream (Stream stream)
    {
        try
        {
            stream.Dispose();
        }
        catch (Exception exception) when (IpcConnectionWriteFailureClassifier.IsConnectionLocalWriteFailure(exception))
        {
        }
    }
}
