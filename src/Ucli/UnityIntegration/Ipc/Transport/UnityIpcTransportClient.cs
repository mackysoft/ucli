using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Transport;

/// <summary> Implements transport-level IPC communication with Unity IPC endpoints. </summary>
internal sealed class UnityIpcTransportClient : IUnityIpcTransportClient
{
    private readonly IIpcEndpointResolver endpointResolver;

    private readonly IIpcTransportClient transportClient;

    /// <summary> Initializes a new instance of the <see cref="UnityIpcTransportClient" /> class. </summary>
    /// <param name="endpointResolver"> The endpoint resolver used to locate Unity IPC endpoints. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="endpointResolver" /> is <see langword="null" />. </exception>
    public UnityIpcTransportClient (
        IIpcEndpointResolver endpointResolver,
        IIpcTransportClient transportClient)
    {
        this.endpointResolver = endpointResolver ?? throw new ArgumentNullException(nameof(endpointResolver));
        this.transportClient = transportClient ?? throw new ArgumentNullException(nameof(transportClient));
    }

    /// <summary> Sends one IPC request to the resolved endpoint and returns its response. </summary>
    /// <param name="storageRoot"> The storage-root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The Unity project fingerprint. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="request"> The request envelope to send. Must not be <see langword="null" />. </param>
    /// <param name="timeout"> The timeout for one IPC request. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The response returned by Unity daemon. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="request" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="storageRoot" /> or <paramref name="projectFingerprint" /> is <see langword="null" />, empty, or whitespace. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    /// <exception cref="TimeoutException"> Thrown when one IPC request exceeds <paramref name="timeout" />. </exception>
    public async ValueTask<IpcResponse> SendAsync (
        string storageRoot,
        string projectFingerprint,
        IpcRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();

        var endpoint = endpointResolver.Resolve(storageRoot, projectFingerprint);
        return await transportClient.SendAsync(endpoint, request, timeout, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<IpcResponse> SendStreamingAsync (
        string storageRoot,
        string projectFingerprint,
        IpcRequest request,
        TimeSpan timeout,
        Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(onProgressFrame);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();

        var endpoint = endpointResolver.Resolve(storageRoot, projectFingerprint);
        return await transportClient.SendStreamingAsync(
                endpoint,
                request,
                timeout,
                onProgressFrame,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
