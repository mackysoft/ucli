using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Helpers.Ipc;

internal sealed class RecordingUnityIpcTransportClient : IUnityIpcTransportClient, IIpcTransportClient
{
    private readonly Func<IpcRequest, CancellationToken, ValueTask<IpcResponse>> responseFactory;

    private readonly Func<IpcRequest, IpcStreamFrame?>? progressFrameFactory;

    private readonly List<UnityInvocation> unityInvocations = [];

    private readonly List<EndpointInvocation> endpointInvocations = [];

    private readonly List<IpcRequest> requests = [];

    private readonly List<IpcRequest> streamingRequests = [];

    public RecordingUnityIpcTransportClient (
        Func<IpcRequest, IpcResponse> responseFactory,
        Func<IpcRequest, IpcStreamFrame?>? progressFrameFactory = null)
        : this(AdaptResponseFactory(responseFactory), progressFrameFactory)
    {
    }

    public RecordingUnityIpcTransportClient (
        Func<IpcRequest, CancellationToken, ValueTask<IpcResponse>> responseFactory,
        Func<IpcRequest, IpcStreamFrame?>? progressFrameFactory = null)
    {
        this.responseFactory = responseFactory ?? throw new ArgumentNullException(nameof(responseFactory));
        this.progressFrameFactory = progressFrameFactory;
    }

    public IReadOnlyList<UnityInvocation> UnityInvocations => unityInvocations;

    public IReadOnlyList<EndpointInvocation> EndpointInvocations => endpointInvocations;

    public IReadOnlyList<IpcRequest> Requests => requests;

    public IReadOnlyList<IpcRequest> StreamingRequests => streamingRequests;

    public ValueTask<IpcResponse> SendAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        IpcRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        unityInvocations.Add(new UnityInvocation(storageRoot, projectFingerprint, request, timeout, IsStreaming: false));
        return SendCoreAsync(request, cancellationToken);
    }

    public async ValueTask<IpcResponse> SendStreamingAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        IpcRequest request,
        TimeSpan timeout,
        Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onProgressFrame);
        cancellationToken.ThrowIfCancellationRequested();
        unityInvocations.Add(new UnityInvocation(storageRoot, projectFingerprint, request, timeout, IsStreaming: true));
        streamingRequests.Add(request);

        await EmitProgressFrameAsync(request, onProgressFrame, cancellationToken).ConfigureAwait(false);

        return await SendCoreAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask<IpcResponse> SendAsync (
        IpcEndpoint endpoint,
        IpcRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        endpointInvocations.Add(new EndpointInvocation(endpoint, request, timeout, IsStreaming: false));
        return SendCoreAsync(request, cancellationToken);
    }

    public async ValueTask<IpcResponse> SendStreamingAsync (
        IpcEndpoint endpoint,
        IpcRequest request,
        TimeSpan timeout,
        Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onProgressFrame);
        cancellationToken.ThrowIfCancellationRequested();
        endpointInvocations.Add(new EndpointInvocation(endpoint, request, timeout, IsStreaming: true));
        streamingRequests.Add(request);

        await EmitProgressFrameAsync(request, onProgressFrame, cancellationToken).ConfigureAwait(false);

        return await SendCoreAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask<IpcResponse> SendWithUnboundedResponseWaitAsync (
        IpcEndpoint endpoint,
        IpcRequest request,
        TimeSpan sendTimeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        endpointInvocations.Add(new EndpointInvocation(endpoint, request, sendTimeout, IsStreaming: false));
        return SendCoreAsync(request, cancellationToken);
    }

    public async ValueTask<IpcResponse> SendStreamingWithUnboundedResponseWaitAsync (
        IpcEndpoint endpoint,
        IpcRequest request,
        TimeSpan sendTimeout,
        Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onProgressFrame);
        cancellationToken.ThrowIfCancellationRequested();
        endpointInvocations.Add(new EndpointInvocation(endpoint, request, sendTimeout, IsStreaming: true));
        streamingRequests.Add(request);

        await EmitProgressFrameAsync(request, onProgressFrame, cancellationToken).ConfigureAwait(false);

        return await SendCoreAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask EmitProgressFrameAsync (
        IpcRequest request,
        Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
        CancellationToken cancellationToken)
    {
        if (progressFrameFactory?.Invoke(request) is { } progressFrame)
        {
            await onProgressFrame(progressFrame, cancellationToken).ConfigureAwait(false);
        }
    }

    private static Func<IpcRequest, CancellationToken, ValueTask<IpcResponse>> AdaptResponseFactory (
        Func<IpcRequest, IpcResponse> responseFactory)
    {
        ArgumentNullException.ThrowIfNull(responseFactory);

        return (request, _) => ValueTask.FromResult(responseFactory(request));
    }

    private ValueTask<IpcResponse> SendCoreAsync (
        IpcRequest request,
        CancellationToken cancellationToken)
    {
        requests.Add(request);
        return responseFactory(request, cancellationToken);
    }

    internal readonly record struct UnityInvocation (
        string StorageRoot,
        ProjectFingerprint ProjectFingerprint,
        IpcRequest Request,
        TimeSpan Timeout,
        bool IsStreaming);

    internal readonly record struct EndpointInvocation (
        IpcEndpoint Endpoint,
        IpcRequest Request,
        TimeSpan Timeout,
        bool IsStreaming);
}
