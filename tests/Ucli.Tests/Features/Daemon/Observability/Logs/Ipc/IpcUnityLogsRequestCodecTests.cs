using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
using MackySoft.Ucli.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Features.Daemon.UseCases.Stop;

namespace MackySoft.Ucli.Tests.Logs;

public sealed class IpcUnityLogsRequestCodecTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void CreateRequest_WithQueryValues_ReturnsUnityLogsReadRequestEnvelope ()
    {
        var query = new IpcUnityLogsReadRequest(
            Tail: 32,
            After: "stream-1:44",
            Since: "2026-03-05T01:00:00Z",
            Until: "2026-03-05T02:00:00Z",
            Level: "warning",
            Query: "socket",
            QueryTarget: "stack",
            Source: "runtime",
            StackTrace: "error",
            StackTraceMaxFrames: 8,
            StackTraceMaxChars: 4096);

        var request = IpcUnityLogsRequestCodec.CreateRequest(query, "session-token");

        Assert.Equal(IpcProtocol.CurrentVersion, request.ProtocolVersion);
        Assert.Equal("session-token", request.SessionToken);
        Assert.Equal(IpcMethodNames.UnityLogsRead, request.Method);
        Assert.True(IpcPayloadCodec.TryDeserialize(request.Payload, out IpcUnityLogsReadRequest payload, out _));
        Assert.Equal(32, payload.Tail);
        Assert.Equal("stream-1:44", payload.After);
        Assert.Equal("2026-03-05T01:00:00Z", payload.Since);
        Assert.Equal("2026-03-05T02:00:00Z", payload.Until);
        Assert.Equal("warning", payload.Level);
        Assert.Equal("socket", payload.Query);
        Assert.Equal("stack", payload.QueryTarget);
        Assert.Equal("runtime", payload.Source);
        Assert.Equal("error", payload.StackTrace);
        Assert.Equal(8, payload.StackTraceMaxFrames);
        Assert.Equal(4096, payload.StackTraceMaxChars);
    }
}