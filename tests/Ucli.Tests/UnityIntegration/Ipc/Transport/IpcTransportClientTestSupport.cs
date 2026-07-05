using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Ipc;

internal static class IpcTransportClientTestSupport
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(1);

    public static readonly TimeSpan UnboundedSendTimeout = TimeSpan.FromMilliseconds(25);

    public static readonly TimeSpan DelayedTerminalFrameWait = TimeSpan.FromMilliseconds(60);

    public static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    public static IpcStreamFrame CreateProgressFrame (
        IpcRequest request,
        int? protocolVersion = null,
        string? requestId = null,
        string kind = IpcStreamFrameKinds.Progress,
        string? eventName = "test.progress",
        IpcResponse? response = null)
    {
        return new IpcStreamFrame(
            protocolVersion ?? IpcProtocol.CurrentVersion,
            requestId ?? request.RequestId,
            kind,
            eventName,
            IpcTransportTestHarness.Json("""{"progress":true}"""),
            response);
    }

    public static IpcStreamFrame CreateProgressFrame (
        string requestId)
    {
        return new IpcStreamFrame(
            IpcProtocol.CurrentVersion,
            requestId,
            IpcStreamFrameKinds.Progress,
            "test.progress",
            IpcTransportTestHarness.Json("""{"progress":true}"""),
            Response: null);
    }

    public static IpcStreamFrame CreateTerminalFrame (
        IpcRequest request,
        string? eventName = null,
        IpcResponse? response = null)
    {
        return new IpcStreamFrame(
            IpcProtocol.CurrentVersion,
            request.RequestId,
            IpcStreamFrameKinds.Terminal,
            eventName,
            IpcTransportTestHarness.Json("{}"),
            response ?? IpcTransportTestHarness.CreateResponse(request.RequestId, """{"done":true}"""));
    }

    public static IpcStreamFrame CreateTerminalFrameWithoutResponse (IpcRequest request)
    {
        return new IpcStreamFrame(
            IpcProtocol.CurrentVersion,
            request.RequestId,
            IpcStreamFrameKinds.Terminal,
            Event: null,
            IpcTransportTestHarness.Json("{}"),
            Response: null);
    }

    public static async Task WriteProgressThenTerminalAsync (
        IpcRequest request,
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
        IpcRequest request,
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
        IpcResponse response)
    {
        var progressFrame = Assert.Single(progressFrames);
        Assert.Equal("test.progress", progressFrame.Event);
        Assert.True(progressFrame.Payload.GetProperty("progress").GetBoolean());
        Assert.Equal("request-1", response.RequestId);
        Assert.True(response.Payload.GetProperty("done").GetBoolean());
    }
}
