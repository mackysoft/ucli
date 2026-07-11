using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Transport;

/// <summary> Sends IPC requests to Unity IPC endpoints over the shared transport protocol. </summary>
internal interface IUnityIpcTransportClient
{
    /// <summary> Sends one request and waits for the corresponding response. </summary>
    /// <param name="storageRoot"> The storage root used to resolve endpoint paths. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The project fingerprint used to resolve endpoint identity. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="request"> The IPC request envelope. Must not be <see langword="null" />. </param>
    /// <param name="timeout"> The timeout for one IPC request. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The response envelope received from the Unity IPC host. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="request" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="storageRoot" /> or <paramref name="projectFingerprint" /> is <see langword="null" />, empty, or whitespace. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    /// <exception cref="IpcResponseReadInterruptedException"> Thrown when request transmission completed but the response frame read was interrupted. </exception>
    ValueTask<IpcResponse> SendAsync (
        string storageRoot,
        string projectFingerprint,
        IpcRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    /// <summary> Sends one request and reads progress frames until the terminal response frame is received. </summary>
    /// <param name="storageRoot"> The storage root used to resolve endpoint paths. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The project fingerprint used to resolve endpoint identity. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="request"> The IPC request envelope. Must not be <see langword="null" />. </param>
    /// <param name="timeout"> The timeout for one IPC request. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="onProgressFrame"> The callback invoked for each progress frame. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The terminal response envelope received from the Unity IPC host. </returns>
    /// <exception cref="IpcResponseReadInterruptedException"> Thrown when request transmission completed but a response stream frame read was interrupted. </exception>
    ValueTask<IpcResponse> SendStreamingAsync (
        string storageRoot,
        string projectFingerprint,
        IpcRequest request,
        TimeSpan timeout,
        Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
        CancellationToken cancellationToken = default);
}
