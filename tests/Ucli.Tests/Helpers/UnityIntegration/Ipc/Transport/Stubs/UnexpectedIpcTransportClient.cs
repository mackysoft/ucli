using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Helpers.Ipc;

internal sealed class UnexpectedIpcTransportClient : IIpcTransportClient
{
    private readonly string reason;

    public UnexpectedIpcTransportClient (string reason)
    {
        this.reason = string.IsNullOrWhiteSpace(reason)
            ? "IPC transport should not be used."
            : reason;
    }

    public ValueTask<IpcResponse> SendAsync (
        IpcEndpoint endpoint,
        IpcRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        throw CreateException(nameof(SendAsync));
    }

    public ValueTask<IpcResponse> SendStreamingAsync (
        IpcEndpoint endpoint,
        IpcRequest request,
        TimeSpan timeout,
        Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
        CancellationToken cancellationToken = default)
    {
        throw CreateException(nameof(SendStreamingAsync));
    }

    public ValueTask<IpcResponse> SendStreamingWithUnboundedResponseWaitAsync (
        IpcEndpoint endpoint,
        IpcRequest request,
        TimeSpan sendTimeout,
        Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
        CancellationToken cancellationToken = default)
    {
        throw CreateException(nameof(SendStreamingWithUnboundedResponseWaitAsync));
    }

    public ValueTask<IpcResponse> SendWithUnboundedResponseWaitAsync (
        IpcEndpoint endpoint,
        IpcRequest request,
        TimeSpan sendTimeout,
        CancellationToken cancellationToken = default)
    {
        throw CreateException(nameof(SendWithUnboundedResponseWaitAsync));
    }

    private InvalidOperationException CreateException (string methodName)
    {
        return new InvalidOperationException($"{methodName} was called unexpectedly. {reason}");
    }
}
