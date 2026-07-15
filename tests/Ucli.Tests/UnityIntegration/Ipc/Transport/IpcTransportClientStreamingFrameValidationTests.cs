using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class IpcTransportClientStreamingFrameValidationTests
{
    public static TheoryData<string, Func<IpcRequestEnvelope, JsonElement>> InvalidStreamingFrames => new()
    {
        {
            "progress request id mismatches",
            request => CreateRawFrame(
                request,
                requestId: Guid.NewGuid())
        },
        {
            "frame kind is unsupported",
            request => CreateRawFrame(
                request,
                kind: "unsupported")
        },
        {
            "progress protocol version mismatches",
            request => CreateRawFrame(
                request,
                protocolVersion: IpcProtocol.CurrentVersion + 1)
        },
        {
            "progress event is missing",
            request => CreateRawFrame(
                request,
                eventName: null)
        },
        {
            "progress contains terminal response",
            request => CreateRawFrame(
                request,
                response: IpcTransportTestHarness.CreateResponse(request.RequestId, "{}"))
        },
        {
            "terminal response is missing",
            request => CreateRawFrame(
                request,
                kind: IpcStreamFrameKind.Terminal,
                eventName: null)
        },
        {
            "terminal contains event",
            request => CreateRawFrame(
                request,
                kind: IpcStreamFrameKind.Terminal,
                eventName: "test.progress",
                response: IpcTransportTestHarness.CreateResponse(request.RequestId, "{}"))
        },
        {
            "terminal response protocol version mismatches",
            request => CreateRawFrame(
                request,
                kind: IpcStreamFrameKind.Terminal,
                eventName: null,
                response: CreateRawResponse(
                    request.RequestId,
                    protocolVersion: IpcProtocol.CurrentVersion + 1))
        },
        {
            "terminal response status is unsupported",
            request => CreateRawFrame(
                request,
                kind: IpcStreamFrameKind.Terminal,
                eventName: null,
                response: CreateRawResponse(
                    request.RequestId,
                    status: "unknown"))
        },
        {
            "terminal response errors is null",
            request => CreateRawFrame(
                request,
                kind: IpcStreamFrameKind.Terminal,
                eventName: null,
                response: CreateRawResponse(
                    request.RequestId,
                    nullErrors: true))
        },
    };

    [Theory]
    [MemberData(nameof(InvalidStreamingFrames))]
    [Trait("Size", "Medium")]
    public async Task SendStreamingAsync_WhenStreamFrameIsInvalid_ThrowsInvalidDataException (
        string caseName,
        Func<IpcRequestEnvelope, JsonElement> createFrame)
    {
        Assert.NotEmpty(caseName);

        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await IpcTransportTestHarness.WithUnixStreamingServerAsync(
            async (request, stream, cancellationToken) =>
            {
                await IpcFrameCodec.WriteModelAsync(
                    stream,
                    createFrame(request),
                    IpcJsonSerializerOptions.Default,
                    cancellationToken: cancellationToken);
            },
            async (endpoint, request) =>
            {
                var client = IpcTransportClientTestSupport.CreateClient(TimeProvider.System);
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

    private static JsonElement CreateRawFrame (
        IpcRequestEnvelope request,
        int? protocolVersion = null,
        Guid? requestId = null,
        object? kind = null,
        string? eventName = "test.progress",
        object? response = null)
    {
        return IpcPayloadCodec.SerializeToElement(new Dictionary<string, object?>
        {
            ["protocolVersion"] = protocolVersion ?? IpcProtocol.CurrentVersion,
            ["requestId"] = requestId ?? request.RequestId,
            ["kind"] = kind ?? IpcStreamFrameKind.Progress,
            ["event"] = eventName,
            ["payload"] = new { progress = true },
            ["response"] = response,
        });
    }

    private static JsonElement CreateRawResponse (
        Guid requestId,
        int? protocolVersion = null,
        object? status = null,
        object? errors = null,
        bool nullErrors = false)
    {
        return IpcPayloadCodec.SerializeToElement(new Dictionary<string, object?>
        {
            ["protocolVersion"] = protocolVersion ?? IpcProtocol.CurrentVersion,
            ["requestId"] = requestId,
            ["status"] = status ?? IpcResponseStatus.Ok,
            ["payload"] = new { },
            ["errors"] = nullErrors ? null : errors ?? Array.Empty<object>(),
        });
    }
}
