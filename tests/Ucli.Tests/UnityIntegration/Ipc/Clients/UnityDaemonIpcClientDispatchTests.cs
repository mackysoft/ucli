using System.Net.Sockets;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Clients;
using static MackySoft.Ucli.Tests.Ipc.UnityDaemonIpcClientTestSupport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityDaemonIpcClientDispatchTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenSuccessful_ResolvesSessionConnectionAndDelegatesToTransport ()
    {
        var response = CreateResponse("req-success");
        var transportClient = new RecordingIpcTransportClient(_ => response);
        var sessionConnectionProvider = new QueuedDaemonSessionConnectionProvider(
            CreateConnectionResult("daemon-token"));
        var client = new UnityDaemonIpcClient(transportClient, sessionConnectionProvider);

        var result = await client.SendAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            CreateDispatchRequest(),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        AssertUnityResponse(response, result.Response);
        var request = DaemonIpcDispatchAssert.SingleDispatchSentToEndpoint(
            transportClient,
            "/tmp/ucli-session.sock",
            IpcMethodNames.OpsRead,
            "daemon-token");
        Assert.Equal(CreateDispatchPayload().GetRawText(), request.Payload.GetRawText());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenSessionTokenIsNotAvailable_ReturnsFailureWithoutCallingTransport ()
    {
        var transportClient = new RecordingIpcTransportClient(_ => CreateResponse("unused"));
        var sessionConnectionProvider = new QueuedDaemonSessionConnectionProvider(
            DaemonSessionConnectionResolutionResult.SessionNotAvailable());
        var client = new UnityDaemonIpcClient(transportClient, sessionConnectionProvider);

        var result = await client.SendAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            CreateDispatchRequest(),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityExecutionModeDecisionErrorCodes.DaemonNotRunning, result.ErrorCode);
        DaemonIpcDispatchAssert.NoDispatchWasSent(transportClient);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenTransportTimesOut_ReturnsIpcTimeout ()
    {
        var transportClient = new RecordingIpcTransportClient(_ => throw new TimeoutException("timed out"));
        var sessionConnectionProvider = new QueuedDaemonSessionConnectionProvider(
            CreateConnectionResult("daemon-token"));
        var client = new UnityDaemonIpcClient(transportClient, sessionConnectionProvider);

        var result = await client.SendAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            CreateDispatchRequest(),
            TimeSpan.FromMilliseconds(500),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendAsync_WhenNonRecoverableDispatchConnectionIsRefused_ReturnsDaemonNotRunningWithoutRetry ()
    {
        var transportClient = new RecordingIpcTransportClient(_ => throw new SocketException((int)SocketError.ConnectionRefused));
        var sessionConnectionProvider = new QueuedDaemonSessionConnectionProvider(
            CreateConnectionResult("daemon-token"));
        var client = new UnityDaemonIpcClient(transportClient, sessionConnectionProvider);

        var result = await client.SendAsync(
            ResolvedUnityProjectContextTestFactory.Create(),
            CreateDispatchRequest(),
            TimeSpan.FromSeconds(5),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(UnityExecutionModeDecisionErrorCodes.DaemonNotRunning, result.ErrorCode);
        DaemonIpcDispatchAssert.SingleDispatchAttempted(transportClient, IpcMethodNames.OpsRead);
    }
}
