using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Helpers.Ipc;

internal sealed class RecordingUnityIpcTransportClient : IUnityIpcTransportClient, IIpcTransportClient
{
    private readonly Func<IpcRequestEnvelope, CancellationToken, ValueTask<IpcResponse>> responseFactory;

    private readonly Func<IpcRequestEnvelope, IpcStreamFrame?>? progressFrameFactory;

    private readonly List<UnityInvocation> unityInvocations = [];

    private readonly List<EndpointInvocation> endpointInvocations = [];

    private readonly List<IpcRequestEnvelope> requests = [];

    private readonly List<IpcRequestEnvelope> streamingRequests = [];

    public RecordingUnityIpcTransportClient (
        Func<IpcRequestEnvelope, IpcResponse> responseFactory,
        Func<IpcRequestEnvelope, IpcStreamFrame?>? progressFrameFactory = null)
        : this(AdaptResponseFactory(responseFactory), progressFrameFactory)
    {
    }

    public RecordingUnityIpcTransportClient (
        Func<IpcRequestEnvelope, CancellationToken, ValueTask<IpcResponse>> responseFactory,
        Func<IpcRequestEnvelope, IpcStreamFrame?>? progressFrameFactory = null)
    {
        this.responseFactory = responseFactory ?? throw new ArgumentNullException(nameof(responseFactory));
        this.progressFrameFactory = progressFrameFactory;
    }

    public IReadOnlyList<UnityInvocation> UnityInvocations => unityInvocations;

    public IReadOnlyList<EndpointInvocation> EndpointInvocations => endpointInvocations;

    public IReadOnlyList<IpcRequestEnvelope> Requests => requests;

    public IReadOnlyList<IpcRequestEnvelope> StreamingRequests => streamingRequests;

    public ValueTask<IpcResponse> SendAsync (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        IpcRequestEnvelope request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        unityInvocations.Add(new UnityInvocation(storageRoot, projectFingerprint, request, timeout, IsStreaming: false));
        return SendCoreAsync(request, cancellationToken);
    }

    public async ValueTask<IpcResponse> SendStreamingAsync (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        IpcRequestEnvelope request,
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
        IpcTransportEndpoint endpoint,
        IpcRequestEnvelope request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        endpointInvocations.Add(new EndpointInvocation(endpoint.Contract, request, timeout, IsStreaming: false));
        return SendCoreAsync(request, cancellationToken);
    }

    public async ValueTask<IpcResponse> SendStreamingAsync (
        IpcTransportEndpoint endpoint,
        IpcRequestEnvelope request,
        TimeSpan timeout,
        Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onProgressFrame);
        cancellationToken.ThrowIfCancellationRequested();
        endpointInvocations.Add(new EndpointInvocation(endpoint.Contract, request, timeout, IsStreaming: true));
        streamingRequests.Add(request);

        await EmitProgressFrameAsync(request, onProgressFrame, cancellationToken).ConfigureAwait(false);

        return await SendCoreAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask<IpcResponse> SendWithUnboundedResponseWaitAsync (
        IpcTransportEndpoint endpoint,
        IpcRequestEnvelope request,
        TimeSpan sendTimeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        endpointInvocations.Add(new EndpointInvocation(endpoint.Contract, request, sendTimeout, IsStreaming: false));
        return SendCoreAsync(request, cancellationToken);
    }

    public async ValueTask<IpcResponse> SendStreamingWithUnboundedResponseWaitAsync (
        IpcTransportEndpoint endpoint,
        IpcRequestEnvelope request,
        TimeSpan sendTimeout,
        Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onProgressFrame);
        cancellationToken.ThrowIfCancellationRequested();
        endpointInvocations.Add(new EndpointInvocation(endpoint.Contract, request, sendTimeout, IsStreaming: true));
        streamingRequests.Add(request);

        await EmitProgressFrameAsync(request, onProgressFrame, cancellationToken).ConfigureAwait(false);

        return await SendCoreAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask EmitProgressFrameAsync (
        IpcRequestEnvelope request,
        Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
        CancellationToken cancellationToken)
    {
        if (progressFrameFactory?.Invoke(request) is { } progressFrame)
        {
            await onProgressFrame(progressFrame, cancellationToken).ConfigureAwait(false);
        }
    }

    private static Func<IpcRequestEnvelope, CancellationToken, ValueTask<IpcResponse>> AdaptResponseFactory (
        Func<IpcRequestEnvelope, IpcResponse> responseFactory)
    {
        ArgumentNullException.ThrowIfNull(responseFactory);

        return (request, _) => ValueTask.FromResult(responseFactory(request));
    }

    private ValueTask<IpcResponse> SendCoreAsync (
        IpcRequestEnvelope request,
        CancellationToken cancellationToken)
    {
        requests.Add(request);
        return responseFactory(request, cancellationToken);
    }

    internal readonly record struct UnityInvocation (
        AbsolutePath StorageRoot,
        ProjectFingerprint ProjectFingerprint,
        IpcRequestEnvelope Request,
        TimeSpan Timeout,
        bool IsStreaming);

    internal readonly record struct EndpointInvocation (
        IpcEndpoint Endpoint,
        IpcRequestEnvelope Request,
        TimeSpan Timeout,
        bool IsStreaming);
}
