using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Transport;

/// <summary> Sends one IPC request to an explicitly resolved transport endpoint. </summary>
internal interface IIpcTransportClient
{
    /// <summary> Sends one IPC request and returns the response envelope. </summary>
    /// <param name="endpoint"> The explicit IPC endpoint. </param>
    /// <param name="request"> The request envelope. </param>
    /// <param name="timeout"> The timeout for one request. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The response envelope. </returns>
    ValueTask<IpcResponse> SendAsync (
        IpcEndpoint endpoint,
        IpcRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends one IPC request with timeout-limited connection and request write, then waits for the response
    /// until the caller cancels or the peer closes the connection.
    /// </summary>
    /// <param name="endpoint"> The explicit IPC endpoint. </param>
    /// <param name="request"> The request envelope. </param>
    /// <param name="sendTimeout"> The timeout for connection and request write. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The response envelope. </returns>
    ValueTask<IpcResponse> SendWithUnboundedResponseWaitAsync (
        IpcEndpoint endpoint,
        IpcRequest request,
        TimeSpan sendTimeout,
        CancellationToken cancellationToken = default);
}
