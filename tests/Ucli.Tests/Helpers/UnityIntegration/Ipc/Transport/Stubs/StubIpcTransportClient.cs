using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
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
        IpcTransportEndpoint endpoint,
        IpcRequestEnvelope request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        Invocations.Add(new StubIpcTransportInvocation(endpoint.Contract, request, timeout, UsesUnboundedResponseWait: false));
        if (SendHandler == null)
        {
            throw new InvalidOperationException("Stub IPC transport handler is not configured.");
        }

        return SendHandler(endpoint.Contract, request, timeout, cancellationToken);
    }

    public ValueTask<IpcResponse> SendWithUnboundedResponseWaitAsync (
        IpcTransportEndpoint endpoint,
        IpcRequestEnvelope request,
        TimeSpan sendTimeout,
        CancellationToken cancellationToken = default)
    {
        Invocations.Add(new StubIpcTransportInvocation(endpoint.Contract, request, sendTimeout, UsesUnboundedResponseWait: true));
        if (SendHandler == null)
        {
            throw new InvalidOperationException("Stub IPC transport handler is not configured.");
        }

        return SendHandler(endpoint.Contract, request, sendTimeout, cancellationToken);
    }

    public ValueTask<IpcResponse> SendStreamingAsync (
        IpcTransportEndpoint endpoint,
        IpcRequestEnvelope request,
        TimeSpan timeout,
        Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
        CancellationToken cancellationToken = default)
    {
        Invocations.Add(new StubIpcTransportInvocation(endpoint.Contract, request, timeout, UsesUnboundedResponseWait: false));
        if (StreamingHandler != null)
        {
            return StreamingHandler(endpoint.Contract, request, timeout, onProgressFrame, cancellationToken);
        }

        if (SendHandler == null)
        {
            throw new InvalidOperationException("Stub IPC transport handler is not configured.");
        }

        return SendHandler(endpoint.Contract, request, timeout, cancellationToken);
    }

    public ValueTask<IpcResponse> SendStreamingWithUnboundedResponseWaitAsync (
        IpcTransportEndpoint endpoint,
        IpcRequestEnvelope request,
        TimeSpan sendTimeout,
        Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
        CancellationToken cancellationToken = default)
    {
        Invocations.Add(new StubIpcTransportInvocation(endpoint.Contract, request, sendTimeout, UsesUnboundedResponseWait: true));
        if (StreamingHandler != null)
        {
            return StreamingHandler(endpoint.Contract, request, sendTimeout, onProgressFrame, cancellationToken);
        }

        if (SendHandler == null)
        {
            throw new InvalidOperationException("Stub IPC transport handler is not configured.");
        }

        return SendHandler(endpoint.Contract, request, sendTimeout, cancellationToken);
    }
}
