using MackySoft.Tests;
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
                var client = new IpcTransportClient();
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
                    "protocol" => IpcTransportTestHarness.CreateResponse(request.RequestId, "{}", protocolVersion: IpcProtocol.CurrentVersion + 1),
                    "requestId" => IpcTransportTestHarness.CreateResponse("other-request", "{}"),
                    "status" => IpcTransportTestHarness.CreateResponse(request.RequestId, "{}", status: "unknown"),
                    "errors" => new IpcResponse(
                        IpcProtocol.CurrentVersion,
                        request.RequestId,
                        IpcProtocol.StatusOk,
                        IpcTransportTestHarness.Json("{}"),
                        null!),
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
                var client = new IpcTransportClient();
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
}
