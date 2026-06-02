using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Infrastructure.Ipc;

/// <summary> Writes length-prefixed IPC stream frames to one connected transport stream. </summary>
internal sealed class IpcStreamFrameWriter : IIpcStreamFrameWriter
{
    private readonly Stream stream;

    private readonly string requestId;

    private readonly Action<Exception>? writeFailureHandler;

    private readonly SemaphoreSlim writeGate = new(1, 1);

    /// <summary> Initializes a new instance of the <see cref="IpcStreamFrameWriter" /> class. </summary>
    /// <param name="stream"> The connected transport stream. </param>
    /// <param name="request"> The request that owns the stream response. </param>
    /// <param name="writeFailureHandler"> The optional callback invoked when a connection-local write failure occurs. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="stream" /> or <paramref name="request" /> is <see langword="null" />. </exception>
    public IpcStreamFrameWriter (
        Stream stream,
        IpcRequest request,
        Action<Exception>? writeFailureHandler = null)
    {
        this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        requestId = request.RequestId;
        this.writeFailureHandler = writeFailureHandler;
    }

    /// <summary> Writes one progress frame. </summary>
    /// <typeparam name="TPayload"> The progress payload type. </typeparam>
    /// <param name="eventName"> The progress event name. </param>
    /// <param name="payload"> The progress payload. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
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
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: requestId,
            Kind: IpcStreamFrameKinds.Progress,
            Event: eventName,
            Payload: IpcPayloadCodec.SerializeToElement(payload),
            Response: null);
        await WriteFrameAsync(frame, cancellationToken).ConfigureAwait(false);
    }

    /// <summary> Writes the terminal response frame. </summary>
    /// <param name="response"> The terminal response. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
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
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: requestId,
            Kind: IpcStreamFrameKinds.Terminal,
            Event: null,
            Payload: IpcPayloadCodec.SerializeToElement(new UcliEmptyArgs()),
            Response: response);
        await WriteFrameAsync(frame, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask WriteFrameAsync (
        IpcStreamFrame frame,
        CancellationToken cancellationToken)
    {
        await writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            try
            {
                await IpcFrameCodec.WriteModelAsync(
                        stream,
                        frame,
                        IpcJsonSerializerOptions.Default,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (IpcConnectionWriteFailureClassifier.IsConnectionLocalWriteFailure(exception))
            {
                writeFailureHandler?.Invoke(exception);
                throw;
            }
        }
        finally
        {
            writeGate.Release();
        }
    }
}
