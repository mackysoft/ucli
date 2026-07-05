using System.Net.Sockets;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonShutdownClientTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task SendShutdown_WhenIpcTimesOut_ReturnsTimeoutFailure ()
    {
        var transportClient = new RecordingIpcTransportClient(static _ => throw new InvalidOperationException("unexpected transport response"));
        transportClient.EnqueueException(new TimeoutException("ipc timeout"));
        var client = new DaemonShutdownClient(transportClient);

        var result = await client.SendShutdownAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-shutdown-timeout"),
            DaemonSessionTestFactory.Create(
                sessionToken: "session-token",
                endpointAddress: "ucli-daemon-test-endpoint"),
            TimeSpan.FromMilliseconds(500),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.IsNotRunning);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendShutdown_WhenSocketConnectionIsRefused_ReturnsNotRunning ()
    {
        var transportClient = new RecordingIpcTransportClient(static _ => throw new InvalidOperationException("unexpected transport response"));
        transportClient.EnqueueException(new SocketException((int)SocketError.ConnectionRefused));
        var client = new DaemonShutdownClient(transportClient);

        var result = await client.SendShutdownAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-shutdown-not-running"),
            DaemonSessionTestFactory.Create(
                sessionToken: "session-token",
                endpointAddress: "ucli-daemon-test-endpoint"),
            TimeSpan.FromMilliseconds(500),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsNotRunning);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task SendShutdown_WhenSocketConnectionReset_ReturnsFailure ()
    {
        var transportClient = new RecordingIpcTransportClient(static _ => throw new InvalidOperationException("unexpected transport response"));
        transportClient.EnqueueException(new SocketException((int)SocketError.ConnectionReset));
        var client = new DaemonShutdownClient(transportClient);

        var result = await client.SendShutdownAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-shutdown-transport-error"),
            DaemonSessionTestFactory.Create(
                sessionToken: "session-token",
                endpointAddress: "ucli-daemon-test-endpoint"),
            TimeSpan.FromMilliseconds(500),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.IsNotRunning);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(nameof(IpcSessionErrorCodes.SessionTokenRequired))]
    [InlineData(nameof(IpcSessionErrorCodes.SessionTokenInvalid))]
    public async Task SendShutdown_WhenSessionAuthenticationIsRejected_ReturnsFailure (string errorCodeName)
    {
        var errorCode = errorCodeName switch
        {
            nameof(IpcSessionErrorCodes.SessionTokenRequired) => IpcSessionErrorCodes.SessionTokenRequired,
            nameof(IpcSessionErrorCodes.SessionTokenInvalid) => IpcSessionErrorCodes.SessionTokenInvalid,
            _ => throw new ArgumentOutOfRangeException(nameof(errorCodeName), errorCodeName, null),
        };
        var transportClient = new RecordingIpcTransportClient(
            request => IpcResponseTestFactory.CreateError(request, errorCode, "session rejected"));
        var client = new DaemonShutdownClient(transportClient);

        var result = await client.SendShutdownAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-shutdown-auth-rejected"),
            DaemonSessionTestFactory.Create(
                sessionToken: "session-token",
                endpointAddress: "ucli-daemon-test-endpoint"),
            TimeSpan.FromMilliseconds(500),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.IsNotRunning);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains(errorCode.Value, error.Message, StringComparison.Ordinal);
        var endpoint = Assert.Single(transportClient.Endpoints);
        Assert.Equal(IpcTransportKind.NamedPipe, endpoint.TransportKind);
        Assert.Equal("ucli-daemon-test-endpoint", endpoint.Address);
    }
}
