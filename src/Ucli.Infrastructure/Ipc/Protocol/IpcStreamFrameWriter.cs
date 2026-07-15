using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Infrastructure.Ipc;

/// <summary> Writes length-prefixed IPC stream frames to one connected transport stream. </summary>
internal sealed class IpcStreamFrameWriter : IIpcStreamFrameWriter, IDisposable
{
    private readonly Stream stream;

    private readonly Guid requestId;

    private readonly CancellationToken connectionLifetimeCancellationToken;

    private readonly CancellationTokenSource connectionLifetimeSignal;

    private readonly Task connectionLifetimeCancellationTask;

    private readonly CancellationToken transportWriteCancellationToken;

    private readonly CancellationToken frameWriteCutoffToken;

    private readonly CancellationTokenSource frameWriteCutoffSignal;

    private readonly Task frameWriteCutoffTask;

    private readonly Action<Exception>? writeFailureHandler;

    private readonly SemaphoreSlim writeGate = new(1, 1);

    private Exception? terminalWriteFailure;

    private bool terminalFrameWritten;

    private int disposed;

    /// <summary> Initializes a new instance of the <see cref="IpcStreamFrameWriter" /> class. </summary>
    /// <param name="stream"> The connected transport stream. </param>
    /// <param name="request"> The response-correlation context that owns the stream response. </param>
    /// <param name="connectionLifetimeCancellationToken"> The cancellation token for the connected transport lifetime. </param>
    /// <param name="transportWriteCancellationToken"> The cancellation token passed to transport write operations. </param>
    /// <param name="frameWriteCutoffToken"> The shared request-phase token canceled at the monotonic write cutoff. </param>
    /// <param name="writeFailureHandler"> The callback invoked when a connection-local write failure occurs, or <see langword="null" /> when no notification is required. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="stream" /> or <paramref name="request" /> is <see langword="null" />. </exception>
    public IpcStreamFrameWriter (
        Stream stream,
        IIpcRequestCorrelation request,
        CancellationToken connectionLifetimeCancellationToken,
        CancellationToken transportWriteCancellationToken,
        CancellationToken frameWriteCutoffToken,
        Action<Exception>? writeFailureHandler)
    {
        this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        requestId = request.RequestId;
        this.connectionLifetimeCancellationToken = connectionLifetimeCancellationToken;
        connectionLifetimeSignal = CancellationTokenSource.CreateLinkedTokenSource(
            connectionLifetimeCancellationToken);
        connectionLifetimeCancellationTask = Task.Delay(
            Timeout.Infinite,
            connectionLifetimeSignal.Token);
        this.transportWriteCancellationToken = transportWriteCancellationToken;
        this.frameWriteCutoffToken = frameWriteCutoffToken;
        frameWriteCutoffSignal = CancellationTokenSource.CreateLinkedTokenSource(frameWriteCutoffToken);
        frameWriteCutoffTask = Task.Delay(Timeout.Infinite, frameWriteCutoffSignal.Token);
        this.writeFailureHandler = writeFailureHandler;
    }

    /// <summary> Releases cancellation registrations and synchronization state owned by this writer. </summary>
    public void Dispose ()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        frameWriteCutoffSignal.Dispose();
        connectionLifetimeSignal.Dispose();
        writeGate.Dispose();
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
            kind: IpcStreamFrameKind.Progress,
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
            kind: IpcStreamFrameKind.Terminal,
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
        await WaitForWriteGateAsync(frameAdmissionCancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfWriterUnavailable();
            ThrowIfTerminalFrameWasWritten();
            try
            {
                // NOTE: Once admitted, a length-prefixed frame must not be interrupted by an execution deadline.
                // Only the connection lifetime and the exchange-wide write cutoff may stop its transport write.
                connectionLifetimeCancellationToken.ThrowIfCancellationRequested();
                ThrowIfWriteCutoffReached();
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
                var completedTask = await Task.WhenAny(
                        writeTask,
                        connectionLifetimeCancellationTask,
                        frameWriteCutoffTask)
                    .ConfigureAwait(false);
                if (ReferenceEquals(completedTask, writeTask))
                {
                    try
                    {
                        await writeTask.ConfigureAwait(false);
                        terminalFrameWritten |= frame.Kind == IpcStreamFrameKind.Terminal;
                        return;
                    }
                    catch (OperationCanceledException) when (
                        !connectionLifetimeCancellationToken.IsCancellationRequested
                        && frameWriteCutoffToken.IsCancellationRequested)
                    {
                        throw RecordWriteCutoffFailure(
                            "Timed out while writing an IPC stream frame before the request write cutoff.");
                    }
                }

                ObserveFault(writeTask);
                if (ReferenceEquals(
                        completedTask,
                        connectionLifetimeCancellationTask)
                    || connectionLifetimeCancellationToken.IsCancellationRequested)
                {
                    connectionLifetimeCancellationToken.ThrowIfCancellationRequested();
                }

                throw RecordWriteCutoffFailure(
                    "Timed out while writing an IPC stream frame before the request write cutoff.");
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

    private async ValueTask WaitForWriteGateAsync (
        CancellationToken frameAdmissionCancellationToken)
    {
        frameAdmissionCancellationToken.ThrowIfCancellationRequested();
        connectionLifetimeCancellationToken.ThrowIfCancellationRequested();
        ThrowIfWriteCutoffReached();
        if (writeGate.Wait(0))
        {
            return;
        }

        using var gateWaitCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            frameAdmissionCancellationToken,
            connectionLifetimeCancellationToken,
            frameWriteCutoffToken);
        try
        {
            await writeGate.WaitAsync(gateWaitCancellationTokenSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (connectionLifetimeCancellationToken.IsCancellationRequested)
        {
            throw RecordTerminalWriteFailure(
                exception,
                notifyWriteFailure: false);
        }
        catch (OperationCanceledException) when (frameWriteCutoffToken.IsCancellationRequested)
        {
            throw RecordWriteCutoffFailure(
                "Timed out while waiting to write an IPC stream frame before the request write cutoff.");
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
        if (Volatile.Read(ref disposed) != 0)
        {
            throw new ObjectDisposedException(nameof(IpcStreamFrameWriter));
        }

        var failure = Volatile.Read(ref terminalWriteFailure);
        if (failure is not null)
        {
            throw failure;
        }
    }

    private void ThrowIfWriteCutoffReached ()
    {
        if (frameWriteCutoffToken.IsCancellationRequested)
        {
            throw RecordWriteCutoffFailure(
                "The IPC request write cutoff has already elapsed.");
        }
    }

    private void ThrowIfTerminalFrameWasWritten ()
    {
        if (terminalFrameWritten)
        {
            throw new InvalidOperationException(
                "The terminal IPC stream frame has already been written for this request.");
        }
    }

    private Exception RecordWriteCutoffFailure (string message)
    {
        return RecordTerminalWriteFailure(
            new IOException(message),
            notifyWriteFailure: true);
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
