using System.Net.Sockets;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonShutdownClientTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task SendShutdown_WhenIpcTimesOut_ReturnsTimeoutFailure ()
    {
        var client = new DaemonShutdownClient(new StubIpcTransportClient(
            static _ => throw new TimeoutException("ipc timeout")));

        var result = await client.SendShutdownAsync(
            CreateContext("fingerprint-shutdown-timeout"),
            CreateSession(),
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
        var client = new DaemonShutdownClient(new StubIpcTransportClient(
            static _ => throw new SocketException((int)SocketError.ConnectionRefused)));

        var result = await client.SendShutdownAsync(
            CreateContext("fingerprint-shutdown-not-running"),
            CreateSession(),
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
        var client = new DaemonShutdownClient(new StubIpcTransportClient(
            static _ => throw new SocketException((int)SocketError.ConnectionReset)));

        var result = await client.SendShutdownAsync(
            CreateContext("fingerprint-shutdown-transport-error"),
            CreateSession(),
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
        var transportClient = new StubIpcTransportClient(
            request => DaemonServiceTestContext.CreateErrorResponse(request, errorCode, "session rejected"));
        var client = new DaemonShutdownClient(transportClient);

        var result = await client.SendShutdownAsync(
            CreateContext("fingerprint-shutdown-auth-rejected"),
            CreateSession(),
            TimeSpan.FromMilliseconds(500),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.IsNotRunning);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains(errorCode.Value, error.Message, StringComparison.Ordinal);
        Assert.Equal(IpcTransportKind.NamedPipe, transportClient.LastEndpoint!.TransportKind);
        Assert.Equal("ucli-daemon-test-endpoint", transportClient.LastEndpoint.Address);
    }

    private static ResolvedUnityProjectContext CreateContext (string fingerprint)
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: "/tmp/unity-project",
            RepositoryRoot: "/tmp/repo-root",
            ProjectFingerprint: fingerprint,
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static DaemonSession CreateSession ()
    {
        return new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: "session-token",
            ProjectFingerprint: "fingerprint",
            IssuedAtUtc: DateTimeOffset.UtcNow,
            EditorMode: "batchmode",
            OwnerKind: "cli",
            CanShutdownProcess: true,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-daemon-test-endpoint",
            ProcessId: 1234,
            ProcessStartedAtUtc: DateTimeOffset.UtcNow,
            OwnerProcessId: 9876);
    }

    private sealed class StubIpcTransportClient : IIpcTransportClient
    {
        private readonly Func<IpcRequest, IpcResponse> responseFactory;

        public StubIpcTransportClient (Func<IpcRequest, IpcResponse> responseFactory)
        {
            this.responseFactory = responseFactory;
        }

        public IpcEndpoint? LastEndpoint { get; private set; }

        public ValueTask<IpcResponse> SendAsync (
            IpcEndpoint endpoint,
            IpcRequest request,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastEndpoint = endpoint;
            var response = responseFactory(request);
            return ValueTask.FromResult(response);
        }

        public async ValueTask<IpcResponse> SendStreamingAsync (
            IpcEndpoint endpoint,
            IpcRequest request,
            TimeSpan timeout,
            Func<IpcStreamFrame, CancellationToken, ValueTask> onProgressFrame,
            CancellationToken cancellationToken = default)
        {
            return await SendAsync(endpoint, request, timeout, cancellationToken);
        }

        public ValueTask<IpcResponse> SendWithUnboundedResponseWaitAsync (
            IpcEndpoint endpoint,
            IpcRequest request,
            TimeSpan sendTimeout,
            CancellationToken cancellationToken = default)
        {
            return SendAsync(endpoint, request, sendTimeout, cancellationToken);
        }
    }
}
