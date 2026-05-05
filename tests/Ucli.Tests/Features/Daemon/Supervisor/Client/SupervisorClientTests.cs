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
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
using MackySoft.Ucli.UnityIntegration.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorClientTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ProbeReachability_WhenNamedPipeConnectTimesOutAndProcessIsDead_ReturnsUnreachable ()
    {
        var transportClient = new MackySoft.Ucli.Tests.Daemon.DaemonServiceTestContext.StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new IpcConnectTimeoutException("connect timeout"),
        };
        var client = new SupervisorClient(transportClient);
        var manifest = new SupervisorInstanceManifest(
            ProcessId: int.MaxValue,
            SessionToken: "supervisor-session-token",
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-supervisor-test",
            IssuedAtUtc: new DateTimeOffset(2026, 03, 12, 0, 0, 0, TimeSpan.Zero));

        var result = await client.ProbeReachability(
            manifest,
            TimeSpan.FromMilliseconds(100),
            CancellationToken.None);

        Assert.Equal(SupervisorReachabilityProbeStatus.Unreachable, result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ProbeReachability_WhenNamedPipeConnectTimesOutAndProcessIsAlive_ReturnsTimedOut ()
    {
        var transportClient = new MackySoft.Ucli.Tests.Daemon.DaemonServiceTestContext.StubIpcTransportClient
        {
            SendHandler = static (_, _, _, _) => throw new IpcConnectTimeoutException("connect timeout"),
        };
        var client = new SupervisorClient(transportClient);
        var manifest = new SupervisorInstanceManifest(
            ProcessId: Environment.ProcessId,
            SessionToken: "supervisor-session-token",
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-supervisor-test",
            IssuedAtUtc: new DateTimeOffset(2026, 03, 12, 0, 0, 0, TimeSpan.Zero));

        var result = await client.ProbeReachability(
            manifest,
            TimeSpan.FromMilliseconds(100),
            CancellationToken.None);

        Assert.Equal(SupervisorReachabilityProbeStatus.TimedOut, result);
    }
}
