using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Host;

/// <summary> Writes length-prefixed supervisor IPC stream frames to one connected transport stream. </summary>
internal sealed class SupervisorIpcStreamFrameWriter
{
    private readonly Stream stream;

    private readonly string requestId;

    private readonly Action<Exception>? writeFailureHandler;

    private readonly SemaphoreSlim writeGate = new(1, 1);

    /// <summary> Initializes a new instance of the <see cref="SupervisorIpcStreamFrameWriter" /> class. </summary>
    public SupervisorIpcStreamFrameWriter (
        Stream stream,
        IpcRequest request,
        Action<Exception>? writeFailureHandler = null)
    {
        this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
        ArgumentNullException.ThrowIfNull(request);

        requestId = request.RequestId;
        this.writeFailureHandler = writeFailureHandler;
    }

    /// <summary> Writes one progress frame. </summary>
    public async ValueTask WriteProgressAsync<TPayload> (
        string eventName,
        TPayload payload,
        CancellationToken cancellationToken = default)
        where TPayload : notnull
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);

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
    public async ValueTask WriteTerminalAsync (
        IpcResponse response,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);

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
            catch (Exception exception) when (IsConnectionLocalWriteFailure(exception))
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

    private static bool IsConnectionLocalWriteFailure (Exception exception)
    {
        return exception is IOException or ObjectDisposedException or InvalidOperationException;
    }
}
