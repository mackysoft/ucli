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
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;

namespace MackySoft.Ucli.Tests.Logs;

public sealed class IpcDaemonLogsRequestCodecTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void CreateRequest_WithQueryValues_ReturnsDaemonLogsReadRequestEnvelope ()
    {
        var query = new IpcDaemonLogsReadRequest(
            Tail: 32,
            After: "stream-1:44",
            Since: "2026-03-05T01:00:00Z",
            Until: "2026-03-05T02:00:00Z",
            Level: "warning",
            Query: "socket",
            QueryTarget: "message",
            Category: "transport");

        var request = IpcDaemonLogsRequestCodec.CreateRequest(query, "session-token");

        Assert.Equal(IpcProtocol.CurrentVersion, request.ProtocolVersion);
        Assert.Equal("session-token", request.SessionToken);
        Assert.Equal(IpcMethodNames.DaemonLogsRead, request.Method);
        Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcDaemonLogsReadRequest payload, out _));
        Assert.Equal(32, payload.Tail);
        Assert.Equal("stream-1:44", payload.After);
        Assert.Equal("2026-03-05T01:00:00Z", payload.Since);
        Assert.Equal("2026-03-05T02:00:00Z", payload.Until);
        Assert.Equal("warning", payload.Level);
        Assert.Equal("socket", payload.Query);
        Assert.Equal("message", payload.QueryTarget);
        Assert.Equal("transport", payload.Category);
    }
}
