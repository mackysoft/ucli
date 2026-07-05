using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class IpcTransportClientStreamingFrameValidationTests
{
    public static TheoryData<string, Func<IpcRequest, IpcStreamFrame>> InvalidStreamingFrames => new()
    {
        {
            "progress request id mismatches",
            _ => IpcTransportClientTestSupport.CreateProgressFrame(requestId: "other-request")
        },
        {
            "frame kind is unsupported",
            request => IpcTransportClientTestSupport.CreateProgressFrame(
                request,
                kind: "unsupported")
        },
        {
            "progress protocol version mismatches",
            request => IpcTransportClientTestSupport.CreateProgressFrame(
                request,
                protocolVersion: IpcProtocol.CurrentVersion + 1)
        },
        {
            "progress event is missing",
            request => IpcTransportClientTestSupport.CreateProgressFrame(
                request,
                eventName: null)
        },
        {
            "progress contains terminal response",
            request => IpcTransportClientTestSupport.CreateProgressFrame(
                request,
                response: IpcTransportTestHarness.CreateResponse(request.RequestId, "{}"))
        },
        {
            "terminal response is missing",
            request => IpcTransportClientTestSupport.CreateTerminalFrameWithoutResponse(request)
        },
        {
            "terminal contains event",
            request => IpcTransportClientTestSupport.CreateTerminalFrame(
                request,
                eventName: "test.progress")
        },
        {
            "terminal response request id mismatches",
            request => IpcTransportClientTestSupport.CreateTerminalFrame(
                request,
                response: IpcTransportTestHarness.CreateResponse("other-request", "{}"))
        },
        {
            "terminal response protocol version mismatches",
            request => IpcTransportClientTestSupport.CreateTerminalFrame(
                request,
                response: IpcTransportTestHarness.CreateResponse(
                    request.RequestId,
                    "{}",
                    protocolVersion: IpcProtocol.CurrentVersion + 1))
        },
        {
            "terminal response status is unsupported",
            request => IpcTransportClientTestSupport.CreateTerminalFrame(
                request,
                response: IpcTransportTestHarness.CreateResponse(
                    request.RequestId,
                    "{}",
                    status: "unknown"))
        },
        {
            "terminal response errors is null",
            request => IpcTransportClientTestSupport.CreateTerminalFrame(
                request,
                response: new IpcResponse(
                    IpcProtocol.CurrentVersion,
                    request.RequestId,
                    IpcProtocol.StatusOk,
                    IpcTransportTestHarness.Json("{}"),
                    null!))
        },
    };

    [Theory]
    [MemberData(nameof(InvalidStreamingFrames))]
    [Trait("Size", "Medium")]
    public async Task SendStreamingAsync_WhenStreamFrameIsInvalid_ThrowsInvalidDataException (
        string caseName,
        Func<IpcRequest, IpcStreamFrame> createFrame)
    {
        Assert.NotEmpty(caseName);

        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await IpcTransportTestHarness.WithUnixStreamingServerAsync(
            async (request, stream, cancellationToken) =>
            {
                await IpcTransportTestHarness.WriteStreamFrameAsync(stream, createFrame(request), cancellationToken);
            },
            async (endpoint, request) =>
            {
                var client = new IpcTransportClient();
                var exceptionTask = Assert.ThrowsAsync<InvalidDataException>(async () =>
                {
                    await client.SendStreamingAsync(
                            endpoint,
                            request,
                            IpcTransportClientTestSupport.DefaultTimeout,
                            (_, _) => ValueTask.CompletedTask)
                        .AsTask();
                });

                await TestAwaiter.WaitAsync(
                    exceptionTask,
                    "IPC streaming frame rejection",
                    IpcTransportClientTestSupport.WaitTimeout);
            },
            IpcTransportClientTestSupport.WaitTimeout);
    }
}
