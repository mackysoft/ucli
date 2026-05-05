using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Application.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Stop;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;
namespace MackySoft.Ucli.Tests.Daemon;

using System.Net.Sockets;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc;

public sealed class DaemonShutdownClientTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task SendShutdown_WhenIpcTimesOut_ReturnsTimeoutFailure ()
    {
        var client = new DaemonShutdownClient(new StubUnityIpcTransportClient(
            static () => throw new TimeoutException("ipc timeout")));

        var result = await client.SendShutdown(
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

        var result = await client.SendShutdown(
            CreateContext("fingerprint-shutdown-not-running"),
            CreateSession(),
            TimeSpan.FromMilliseconds(500),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsNotRunning);
        Assert.Null(result.Error);
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
            RuntimeKind: DaemonSession.RuntimeKindBatchmode,
            OwnerKind: DaemonSession.OwnerKindSupervisor,
            CanShutdownProcess: true,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-daemon-test-endpoint",
            ProcessId: 1234,

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
