using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Ipc;

internal static class IpcTransportClientTestSupport
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(1);

    public static readonly TimeSpan UnboundedSendTimeout = TimeSpan.FromMilliseconds(25);

    public static readonly TimeSpan DelayedTerminalFrameWait = TimeSpan.FromMilliseconds(60);

    public static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    public static IpcTransportClient CreateClient (TimeProvider timeProvider)
    {
        return new IpcTransportClient(
            new IpcTransportConnector(),
            timeProvider);
    }

    public static IpcStreamFrame CreateProgressFrame (IpcRequestEnvelope request)
    {
        return new IpcStreamFrame(
            IpcProtocol.CurrentVersion,
            request.RequestId,
            IpcStreamFrameKind.Progress,
            "test.progress",
            IpcTransportTestHarness.Json("""{"progress":true}"""),
            response: null);
    }

    public static IpcStreamFrame CreateTerminalFrame (
        IpcRequestEnvelope request,
        IpcResponse? response = null)
    {
        return new IpcStreamFrame(
            IpcProtocol.CurrentVersion,
            request.RequestId,
            IpcStreamFrameKind.Terminal,
            @event: null,
            IpcTransportTestHarness.Json("{}"),
            response ?? IpcTransportTestHarness.CreateResponse(request.RequestId, """{"done":true}"""));
    }

    public static async Task WriteProgressThenTerminalAsync (
        IpcRequestEnvelope request,
        Stream stream,
        CancellationToken cancellationToken)
    {
        await IpcTransportTestHarness.WriteStreamFrameAsync(
            stream,
            CreateProgressFrame(request),
            cancellationToken);
        await IpcTransportTestHarness.WriteStreamFrameAsync(
            stream,
            CreateTerminalFrame(request),
            cancellationToken);
    }

    public static async Task WriteProgressThenDelayedTerminalAsync (
        IpcRequestEnvelope request,
        Stream stream,
        CancellationToken cancellationToken)
    {
        await IpcTransportTestHarness.WriteStreamFrameAsync(
            stream,
            CreateProgressFrame(request),
            cancellationToken);
        await Task.Delay(DelayedTerminalFrameWait, cancellationToken);
        await IpcTransportTestHarness.WriteStreamFrameAsync(
            stream,
            CreateTerminalFrame(request),
            cancellationToken);
    }

    public static void AssertProgressThenTerminalResult (
        IReadOnlyList<IpcStreamFrame> progressFrames,
        IpcResponse response,
        Guid expectedRequestId)
    {
        var progressFrame = Assert.Single(progressFrames);
        Assert.Equal("test.progress", progressFrame.Event);
        Assert.True(progressFrame.Payload.GetProperty("progress").GetBoolean());
        Assert.Equal(expectedRequestId, response.RequestId);
        Assert.True(response.Payload.GetProperty("done").GetBoolean());
    }
}
