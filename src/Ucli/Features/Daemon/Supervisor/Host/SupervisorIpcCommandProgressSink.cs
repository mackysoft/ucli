using MackySoft.Ucli.Application.Shared.Execution.Progress;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Host;

/// <summary> Projects command progress entries to supervisor IPC progress frames. </summary>
internal sealed class SupervisorIpcCommandProgressSink : ICommandProgressSink
{
    private readonly SupervisorIpcStreamFrameWriter streamWriter;

    /// <summary> Initializes a new instance of the <see cref="SupervisorIpcCommandProgressSink" /> class. </summary>
    public SupervisorIpcCommandProgressSink (SupervisorIpcStreamFrameWriter streamWriter)
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
        catch (Exception exception) when (IsConnectionLocalWriteFailure(exception))
        {
            throw new OperationCanceledException(
                "Supervisor progress stream was canceled because the caller disconnected.",
                exception,
                cancellationToken);
        }
    }

    private static bool IsConnectionLocalWriteFailure (Exception exception)
    {
        return exception is IOException or ObjectDisposedException or InvalidOperationException;
    }
}
