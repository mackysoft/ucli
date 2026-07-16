using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class IpcTransportClientResponseValidationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Size", "Medium")]
    public async Task SendSingleResponseAsync_WhenServerClosesAfterReadingRequest_ThrowsResponseReadInterrupted (
        bool useUnboundedResponseWait)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await IpcTransportTestHarness.WithUnixResponseServerAsync(
            static (_, _, _) => Task.CompletedTask,
            async (endpoint, request) =>
            {
                var client = IpcTransportClientTestSupport.CreateClient(TimeProvider.System);
                var exceptionTask = Assert.ThrowsAsync<IpcResponseReadInterruptedException>(async () =>
                {
                    if (useUnboundedResponseWait)
                    {
                        await client.SendWithUnboundedResponseWaitAsync(
                                endpoint,
                                request,
                                IpcTransportClientTestSupport.DefaultTimeout)
                            .AsTask();
                    }
                    else
                    {
                        await client.SendAsync(
                                endpoint,
                                request,
                                IpcTransportClientTestSupport.DefaultTimeout)
                            .AsTask();
                    }
                });

                var exception = await TestAwaiter.WaitAsync(
                    exceptionTask,
                    "Interrupted IPC response read",
                    IpcTransportClientTestSupport.WaitTimeout);
                Assert.IsType<EndOfStreamException>(exception.InnerException);
            },
            IpcTransportClientTestSupport.WaitTimeout);
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData("protocol")]
    [InlineData("requestId")]
    [InlineData("status")]
    [InlineData("errors")]
    public async Task SendAsync_WhenResponseEnvelopeIsInvalid_ThrowsInvalidDataException (string invalidField)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await IpcTransportTestHarness.WithUnixResponseServerAsync(
            async (request, stream, cancellationToken) =>
            {
                var response = invalidField switch
                {
                    "protocol" => IpcPayloadCodec.SerializeToElement(
                        IpcTransportTestHarness.CreateResponse(
                            request.RequestId,
                            "{}",
                            protocolVersion: IpcProtocol.CurrentVersion + 1)),
                    "requestId" => IpcPayloadCodec.SerializeToElement(
                        IpcTransportTestHarness.CreateResponse(Guid.NewGuid(), "{}")),
                    "status" => CreateRawResponse(request.RequestId, "unknown", Array.Empty<object>()),
                    "errors" => CreateRawResponse(request.RequestId, "ok", errors: null),
                    _ => throw new InvalidOperationException("Unsupported invalid field."),
                };
                await IpcFrameCodec.WriteModelAsync(
                    stream,
                    response,
                    IpcJsonSerializerOptions.Default,
                    cancellationToken: cancellationToken);
            },
            async (endpoint, request) =>
            {
                var client = IpcTransportClientTestSupport.CreateClient(TimeProvider.System);
                var exceptionTask = Assert.ThrowsAsync<InvalidDataException>(async () =>
                {
                    await client.SendAsync(endpoint, request, IpcTransportClientTestSupport.DefaultTimeout).AsTask();
                });

                await TestAwaiter.WaitAsync(
                    exceptionTask,
                    "Invalid IPC response envelope rejection",
                    IpcTransportClientTestSupport.WaitTimeout);
            },
            IpcTransportClientTestSupport.WaitTimeout);
    }

    private static JsonElement CreateRawResponse (
        Guid requestId,
        string status,
        object? errors)
    {
        return IpcPayloadCodec.SerializeToElement(new Dictionary<string, object?>
        {
            ["protocolVersion"] = IpcProtocol.CurrentVersion,
            ["requestId"] = requestId,
            ["status"] = status,
            ["payload"] = new { },
            ["errors"] = errors,
        });
    }
}
