using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Ipc;

/// <summary> Sends IPC requests to Unity daemon endpoints. </summary>
internal interface IUnityIpcClient
{
    /// <summary> Sends one request and waits for the corresponding response. </summary>
    /// <param name="projectRoot"> The Unity project root used to resolve endpoint paths. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The project fingerprint used to resolve endpoint identity. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="request"> The IPC request envelope. Must not be <see langword="null" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The response envelope received from Unity daemon. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="request" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="projectRoot" /> or <paramref name="projectFingerprint" /> is <see langword="null" />, empty, or whitespace. </exception>
    ValueTask<IpcResponse> SendAsync (
        string projectRoot,
        string projectFingerprint,
        IpcRequest request,
        CancellationToken cancellationToken = default);
}
