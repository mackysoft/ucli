using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Helpers.Ipc;

internal sealed record StubIpcTransportInvocation (
    IpcEndpoint Endpoint,
    IpcRequestEnvelope Request,
    TimeSpan Timeout,
    bool UsesUnboundedResponseWait);

internal sealed class StubIpcTransportClient : IIpcTransportClient
{
    public Func<IpcEndpoint, IpcRequestEnvelope, TimeSpan, CancellationToken, ValueTask<IpcResponse>>? SendHandler { get; set; }

    public Func<IpcEndpoint, IpcRequestEnvelope, TimeSpan, Func<IpcStreamFrame, CancellationToken, ValueTask>, CancellationToken, ValueTask<IpcResponse>>? StreamingHandler { get; set; }

    public List<StubIpcTransportInvocation> Invocations { get; } = [];

    public ValueTask<IpcResponse> SendAsync (
        IpcEndpoint endpoint,
        IpcRequestEnvelope request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        Invocations.Add(new StubIpcTransportInvocation(endpoint, request, timeout, UsesUnboundedResponseWait: false));
        if (SendHandler == null)
        {
            throw new InvalidOperationException("Stub IPC transport handler is not configured.");
        }

        return SendHandler(endpoint, request, timeout, cancellationToken);
    }

    public ValueTask<IpcResponse> SendWithUnboundedResponseWaitAsync (
        IpcEndpoint endpoint,
        IpcRequestEnvelope request,
        TimeSpan sendTimeout,
        CancellationToken cancellationToken = default)
    {
        Invocations.Add(new StubIpcTransportInvocation(endpoint, request, sendTimeout, UsesUnboundedResponseWait: true));
        if (SendHandler == null)
        {
            throw new InvalidOperationException("Stub IPC transport handler is not configured.");
        }

        return SendHandler(endpoint, request, sendTimeout, cancellationToken);
    }

    public ValueTask<IpcResponse> SendStreamingAsync (
        IpcEndpoint endpoint,
        IpcRequestEnvelope request,
        TimeSpan timeout,
        Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
        CancellationToken cancellationToken = default)
    {
        Invocations.Add(new StubIpcTransportInvocation(endpoint, request, timeout, UsesUnboundedResponseWait: false));
        if (StreamingHandler != null)
        {
            return StreamingHandler(endpoint, request, timeout, onProgressFrame, cancellationToken);
        }

        if (SendHandler == null)
        {
            throw new InvalidOperationException("Stub IPC transport handler is not configured.");
        }

        return SendHandler(endpoint, request, timeout, cancellationToken);
    }

    public ValueTask<IpcResponse> SendStreamingWithUnboundedResponseWaitAsync (
        IpcEndpoint endpoint,
        IpcRequestEnvelope request,
        TimeSpan sendTimeout,
        Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
        CancellationToken cancellationToken = default)
    {
        Invocations.Add(new StubIpcTransportInvocation(endpoint, request, sendTimeout, UsesUnboundedResponseWait: true));
        if (StreamingHandler != null)
        {
            return StreamingHandler(endpoint, request, sendTimeout, onProgressFrame, cancellationToken);
        }

        if (SendHandler == null)
        {
            throw new InvalidOperationException("Stub IPC transport handler is not configured.");
        }

        return SendHandler(endpoint, request, sendTimeout, cancellationToken);
    }
}
