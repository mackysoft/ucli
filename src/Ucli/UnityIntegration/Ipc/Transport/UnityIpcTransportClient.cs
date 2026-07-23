using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Transport;

/// <summary> Implements transport-level IPC communication with Unity IPC endpoints. </summary>
internal sealed class UnityIpcTransportClient : IUnityIpcTransportClient
{
    private readonly IIpcTransportClient transportClient;

    /// <summary> Initializes a new instance of the <see cref="UnityIpcTransportClient" /> class. </summary>
    /// <param name="transportClient"> The IPC transport client dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="transportClient" /> is <see langword="null" />. </exception>
    public UnityIpcTransportClient (IIpcTransportClient transportClient)
    {
        this.transportClient = transportClient ?? throw new ArgumentNullException(nameof(transportClient));
    }

    /// <summary> Sends one IPC request to the resolved endpoint and returns its response. </summary>
    /// <param name="storageRoot"> The guarded storage-root path. </param>
    /// <param name="projectFingerprint"> The guarded Unity project fingerprint. </param>
    /// <param name="request"> The request envelope to send. Must not be <see langword="null" />. </param>
    /// <param name="timeout"> The timeout for one IPC request. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The response returned by Unity daemon. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="projectFingerprint" /> or <paramref name="request" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />. </exception>
    /// <exception cref="TimeoutException"> Thrown when one IPC request exceeds <paramref name="timeout" />. </exception>
    /// <exception cref="IpcResponseReadInterruptedException"> Thrown when request transmission completed but the response frame read was interrupted. </exception>
    public async ValueTask<IpcResponse> SendAsync (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        IpcRequestEnvelope request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();

        var endpoint = UcliIpcEndpointResolver.ResolveDaemonEndpoint(storageRoot, projectFingerprint);
        return await transportClient.SendAsync(endpoint, request, timeout, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<IpcResponse> SendStreamingAsync (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        IpcRequestEnvelope request,
        TimeSpan timeout,
        Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(onProgressFrame);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();

        var endpoint = UcliIpcEndpointResolver.ResolveDaemonEndpoint(storageRoot, projectFingerprint);
        return await transportClient.SendStreamingAsync(
                endpoint,
                request,
                timeout,
                onProgressFrame,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
