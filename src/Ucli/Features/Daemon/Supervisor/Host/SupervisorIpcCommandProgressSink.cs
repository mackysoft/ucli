using MackySoft.Ucli.Application.Shared.Execution.Progress;
using MackySoft.Ucli.Infrastructure.Ipc;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Host;

/// <summary> Projects command progress entries to supervisor IPC progress frames. </summary>
internal sealed class SupervisorIpcCommandProgressSink : ICommandProgressSink
{
    private readonly IpcStreamFrameWriter streamWriter;

    /// <summary> Initializes a new instance of the <see cref="SupervisorIpcCommandProgressSink" /> class. </summary>
    public SupervisorIpcCommandProgressSink (IpcStreamFrameWriter streamWriter)
    {
        this.streamWriter = streamWriter ?? throw new ArgumentNullException(nameof(streamWriter));
    }

    /// <inheritdoc />
    public async ValueTask OnEntryAsync<TPayload> (
        string eventName,
        TPayload payload,
        CancellationToken cancellationToken = default)
        where TPayload : notnull
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await streamWriter.WriteProgressAsync(eventName, payload, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IpcConnectionWriteFailureClassifier.IsConnectionLocalWriteFailure(exception))
        {
            throw new OperationCanceledException(
                "Supervisor progress stream was canceled because the caller disconnected.",
                exception,
                cancellationToken);
        }
    }
}
