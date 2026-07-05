using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class IpcTransportClientResponseValidationTests
{
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
            request => invalidField switch
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
