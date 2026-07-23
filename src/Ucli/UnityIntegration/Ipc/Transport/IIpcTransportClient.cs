using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;

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
    /// <exception cref="IpcConnectException"> Thrown when a non-timeout connection failure occurs before any request bytes are written. </exception>
    /// <exception cref="IpcConnectTimeoutException"> Thrown when connection does not complete within the shorter of <paramref name="timeout" /> and the transport connection-attempt cap. No request bytes have been written when this exception is thrown. </exception>
    /// <exception cref="TimeoutException"> Thrown when request transmission or response reading does not complete within <paramref name="timeout" />. </exception>
    /// <exception cref="OperationCanceledException"> Thrown when <paramref name="cancellationToken" /> is canceled. </exception>
    /// <exception cref="IpcResponseReadInterruptedException"> Thrown when request transmission completed but the response frame read was interrupted. </exception>
    ValueTask<IpcResponse> SendAsync (
        IpcTransportEndpoint endpoint,
        IpcRequestEnvelope request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    /// <summary> Sends one IPC request and reads progress frames until the terminal response frame is received. </summary>
    /// <param name="endpoint"> The explicit IPC endpoint. </param>
    /// <param name="request"> The request envelope. </param>
    /// <param name="timeout"> The timeout for one request. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="onProgressFrame"> The callback invoked for each progress frame. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The terminal response envelope. </returns>
    /// <exception cref="IpcConnectException"> Thrown when a non-timeout connection failure occurs before any request bytes are written. </exception>
    /// <exception cref="IpcConnectTimeoutException"> Thrown when connection does not complete within the shorter of <paramref name="timeout" /> and the transport connection-attempt cap. No request bytes have been written when this exception is thrown. </exception>
    /// <exception cref="TimeoutException"> Thrown when request transmission or response reading does not complete within <paramref name="timeout" />. </exception>
    /// <exception cref="OperationCanceledException"> Thrown when <paramref name="cancellationToken" /> is canceled. </exception>
    /// <exception cref="IpcResponseReadInterruptedException"> Thrown when request transmission completed but a response stream frame read was interrupted. </exception>
    /// <exception cref="IpcStreamingOperationInProgressException"> Thrown when another streaming operation on this client is active or has not finished after cancellation or timeout. </exception>
    ValueTask<IpcResponse> SendStreamingAsync (
        IpcTransportEndpoint endpoint,
        IpcRequestEnvelope request,
        TimeSpan timeout,
        Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends one streaming IPC request with timeout-limited connection and request write, then reads progress frames
    /// until the terminal response frame is received, the peer closes, or the caller cancels.
    /// </summary>
    /// <param name="endpoint"> The explicit IPC endpoint. </param>
    /// <param name="request"> The request envelope. </param>
    /// <param name="sendTimeout"> The timeout for connection and request write. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="onProgressFrame"> The callback invoked for each progress frame. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The terminal response envelope. </returns>
    /// <exception cref="IpcConnectException"> Thrown when a non-timeout connection failure occurs before any request bytes are written. </exception>
    /// <exception cref="IpcConnectTimeoutException"> Thrown when connection does not complete within the shorter of <paramref name="sendTimeout" /> and the transport connection-attempt cap. No request bytes have been written when this exception is thrown. </exception>
    /// <exception cref="TimeoutException"> Thrown when request transmission does not complete within <paramref name="sendTimeout" />. </exception>
    /// <exception cref="OperationCanceledException"> Thrown when <paramref name="cancellationToken" /> is canceled during connection, writing, or the otherwise unbounded response wait. </exception>
    /// <exception cref="IpcResponseReadInterruptedException"> Thrown when request transmission completed but a response stream frame read was interrupted. </exception>
    /// <exception cref="IpcStreamingOperationInProgressException"> Thrown when another streaming operation on this client is active or has not finished after cancellation. </exception>
    ValueTask<IpcResponse> SendStreamingWithUnboundedResponseWaitAsync (
        IpcTransportEndpoint endpoint,
        IpcRequestEnvelope request,
        TimeSpan sendTimeout,
        Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
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
    /// <exception cref="IpcConnectException"> Thrown when a non-timeout connection failure occurs before any request bytes are written. </exception>
    /// <exception cref="IpcConnectTimeoutException"> Thrown when connection does not complete within the shorter of <paramref name="sendTimeout" /> and the transport connection-attempt cap. No request bytes have been written when this exception is thrown. </exception>
    /// <exception cref="TimeoutException"> Thrown when request transmission does not complete within <paramref name="sendTimeout" />. </exception>
    /// <exception cref="OperationCanceledException"> Thrown when <paramref name="cancellationToken" /> is canceled during connection, writing, or the otherwise unbounded response wait. </exception>
    /// <exception cref="IpcResponseReadInterruptedException"> Thrown when request transmission completed but the response frame read was interrupted. </exception>
    ValueTask<IpcResponse> SendWithUnboundedResponseWaitAsync (
        IpcTransportEndpoint endpoint,
        IpcRequestEnvelope request,
        TimeSpan sendTimeout,
        CancellationToken cancellationToken = default);
}
