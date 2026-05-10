using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;
namespace MackySoft.Ucli.Tests.Daemon;

using System.Net.Sockets;
using System.Text.Json;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

public sealed class DaemonShutdownClientTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task SendShutdown_WhenIpcTimesOut_ReturnsTimeoutFailure ()
    {
        var client = new DaemonShutdownClient(new StubUnityIpcTransportClient(
            static () => throw new TimeoutException("ipc timeout")));

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
        var client = new DaemonShutdownClient(new StubUnityIpcTransportClient(
            static () => throw new SocketException((int)SocketError.ConnectionRefused)));

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
        var client = new DaemonShutdownClient(new StubUnityIpcTransportClient(
            static () => throw new SocketException((int)SocketError.ConnectionReset)));

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
        var client = new DaemonShutdownClient(new StubUnityIpcTransportClient(
            () => CreateErrorResponse(errorCode)));

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
    }

    private static IpcResponse CreateErrorResponse (UcliErrorCode errorCode)
    {
        return new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: "req-shutdown-error",
            Status: IpcProtocol.StatusError,
            Payload: JsonDocument.Parse("{}").RootElement.Clone(),
            Errors:
            [
                new IpcError(errorCode, "session rejected", null),
            ]);
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
            EditorMode: DaemonEditorModeValues.Batchmode,
            OwnerKind: DaemonSessionOwnerKindValues.Cli,
            CanShutdownProcess: true,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-daemon-test-endpoint",
            ProcessId: 1234,
            ProcessStartedAtUtc: DateTimeOffset.UtcNow,
            OwnerProcessId: 9876);
    }

    private sealed class StubUnityIpcTransportClient : IUnityIpcTransportClient
    {
        private readonly Func<IpcResponse> responseFactory;

        public StubUnityIpcTransportClient (Func<IpcResponse> responseFactory)
        {
            this.responseFactory = responseFactory;
        }

        public ValueTask<IpcResponse> SendAsync (
            string storageRoot,
            string projectFingerprint,
            IpcRequest request,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            var response = responseFactory();
            return ValueTask.FromResult(response);
        }
    }
}
