using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Infrastructure.Ipc;

/// <summary> Writes progress and terminal frames for one streaming IPC request. </summary>
internal interface IIpcStreamFrameWriter
{
    /// <summary> Writes one progress frame. </summary>
    /// <typeparam name="TPayload"> The progress payload type. </typeparam>
    /// <param name="eventName"> The progress event name. </param>
    /// <param name="payload"> The progress payload. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
    /// <returns> A task that completes after the frame is flushed. </returns>
    ValueTask WriteProgressAsync<TPayload> (
        string eventName,
        TPayload payload,
        CancellationToken cancellationToken = default)
        where TPayload : notnull;

    /// <summary> Writes the terminal response frame. </summary>
    /// <param name="response"> The terminal response. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by request execution. </param>
    /// <returns> A task that completes after the frame is flushed. </returns>
    ValueTask WriteTerminalAsync (
        IpcResponse response,
        CancellationToken cancellationToken = default);
}
