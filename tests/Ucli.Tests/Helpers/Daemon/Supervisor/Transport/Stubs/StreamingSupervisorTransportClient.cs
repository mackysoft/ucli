using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal sealed class StreamingSupervisorTransportClient : IIpcTransportClient
{
    private readonly Func<IpcRequest, Func<IpcStreamFrame, CancellationToken, ValueTask>, CancellationToken, ValueTask<IpcResponse>> streamingHandler;
    private readonly List<StreamingSupervisorTransportCall> streamingCalls = [];

    public StreamingSupervisorTransportClient (
        Func<IpcRequest, Func<IpcStreamFrame, CancellationToken, ValueTask>, CancellationToken, ValueTask<IpcResponse>> streamingHandler)
    {
        this.streamingHandler = streamingHandler;
    }

    public void AssertEnsureRunningStreamingRequested (TimeSpan? expectedTimeout = null)
    {
        var call = Assert.Single(streamingCalls);
        Assert.Equal(ContractLiteralCodec.ToValue(SupervisorIpcMethod.EnsureRunning), call.Request.Method);
        Assert.Equal(ContractLiteralCodec.ToValue(IpcResponseMode.Stream), call.Request.ResponseMode);
        Assert.True(call.UsesUnboundedResponseWait);
        if (expectedTimeout.HasValue)
        {
            Assert.Equal(expectedTimeout.Value, call.Timeout);
        }
    }

    public ValueTask<IpcResponse> SendAsync (
        IpcEndpoint endpoint,
        IpcRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Expected streaming supervisor transport.");
    }

    public ValueTask<IpcResponse> SendWithUnboundedResponseWaitAsync (
        IpcEndpoint endpoint,
        IpcRequest request,
        TimeSpan sendTimeout,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Expected streaming supervisor transport.");
    }

    public ValueTask<IpcResponse> SendStreamingAsync (
        IpcEndpoint endpoint,
        IpcRequest request,
        TimeSpan timeout,
        Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Expected unbounded streaming supervisor transport.");
    }

    public ValueTask<IpcResponse> SendStreamingWithUnboundedResponseWaitAsync (
        IpcEndpoint endpoint,
        IpcRequest request,
        TimeSpan sendTimeout,
        Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
        CancellationToken cancellationToken = default)
    {
        streamingCalls.Add(new StreamingSupervisorTransportCall(request, sendTimeout, UsesUnboundedResponseWait: true));
        return streamingHandler(request, onProgressFrame, cancellationToken);
    }

    private sealed record StreamingSupervisorTransportCall (
        IpcRequest Request,
        TimeSpan Timeout,
        bool UsesUnboundedResponseWait);
}
