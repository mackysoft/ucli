using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Helpers.Ipc;

internal sealed class RecordingIpcTransportClient : IIpcTransportClient
{
    private readonly Func<IpcRequestEnvelope, IpcResponse> defaultResponseFactory;

    private readonly Func<IpcRequestEnvelope, IpcStreamFrame?>? progressFrameFactory;

    private readonly Queue<Func<IpcRequestEnvelope, IpcResponse>> responses = [];

    private readonly Queue<Exception> exceptions = [];

    private readonly List<IpcEndpoint> endpoints = [];

    private readonly List<IpcRequestEnvelope> requests = [];

    private readonly List<IpcRequestEnvelope> streamingRequests = [];

    private readonly List<TimeSpan> timeouts = [];

    private readonly List<CancellationToken> cancellationTokens = [];

    public RecordingIpcTransportClient (
        Func<IpcRequestEnvelope, IpcResponse> responseFactory,
        Func<IpcRequestEnvelope, IpcStreamFrame?>? progressFrameFactory = null)
    {
        defaultResponseFactory = responseFactory ?? throw new ArgumentNullException(nameof(responseFactory));
        this.progressFrameFactory = progressFrameFactory;
    }

    public IReadOnlyList<IpcEndpoint> Endpoints => endpoints;

    public IReadOnlyList<IpcRequestEnvelope> Requests => requests;

    public IReadOnlyList<IpcRequestEnvelope> StreamingRequests => streamingRequests;

    public IReadOnlyList<TimeSpan> Timeouts => timeouts;

    public IReadOnlyList<CancellationToken> CancellationTokens => cancellationTokens;

    public void EnqueueResponse (Func<IpcRequestEnvelope, IpcResponse> response)
    {
        responses.Enqueue(response ?? throw new ArgumentNullException(nameof(response)));
    }

    public void EnqueueResponse (IpcResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        responses.Enqueue(_ => response);
    }

    public void EnqueueException (Exception exception)
    {
        exceptions.Enqueue(exception ?? throw new ArgumentNullException(nameof(exception)));
    }

    public ValueTask<IpcResponse> SendAsync (
        IpcTransportEndpoint endpoint,
        IpcRequestEnvelope request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        return SendCore(endpoint, request, timeout, cancellationToken);
    }

    public ValueTask<IpcResponse> SendStreamingAsync (
        IpcTransportEndpoint endpoint,
        IpcRequestEnvelope request,
        TimeSpan timeout,
        Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
        CancellationToken cancellationToken = default)
    {
        return SendStreamingCore(endpoint, request, timeout, onProgressFrame, cancellationToken);
    }

    public ValueTask<IpcResponse> SendStreamingWithUnboundedResponseWaitAsync (
        IpcTransportEndpoint endpoint,
        IpcRequestEnvelope request,
        TimeSpan sendTimeout,
        Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
        CancellationToken cancellationToken = default)
    {
        return SendStreamingCore(endpoint, request, sendTimeout, onProgressFrame, cancellationToken);
    }

    public ValueTask<IpcResponse> SendWithUnboundedResponseWaitAsync (
        IpcTransportEndpoint endpoint,
        IpcRequestEnvelope request,
        TimeSpan sendTimeout,
        CancellationToken cancellationToken = default)
    {
        return SendCore(endpoint, request, sendTimeout, cancellationToken);
    }

    private ValueTask<IpcResponse> SendCore (
        IpcTransportEndpoint endpoint,
        IpcRequestEnvelope request,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        endpoints.Add(endpoint.Contract);
        requests.Add(request);
        timeouts.Add(timeout);
        cancellationTokens.Add(cancellationToken);

        if (exceptions.Count != 0)
        {
            throw exceptions.Dequeue();
        }

        return ValueTask.FromResult(responses.Count == 0
            ? defaultResponseFactory(request)
            : responses.Dequeue()(request));
    }

    private async ValueTask<IpcResponse> SendStreamingCore (
        IpcTransportEndpoint endpoint,
        IpcRequestEnvelope request,
        TimeSpan timeout,
        Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(onProgressFrame);
        cancellationToken.ThrowIfCancellationRequested();
        streamingRequests.Add(request);

        if (progressFrameFactory?.Invoke(request) is { } progressFrame)
        {
            await onProgressFrame(progressFrame, cancellationToken).ConfigureAwait(false);
        }

        return await SendCore(endpoint, request, timeout, cancellationToken).ConfigureAwait(false);
    }
}
