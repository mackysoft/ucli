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
using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Tests.Logs;

public sealed class IpcDaemonLogsResponseCodecTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryDecode_WhenResponseIsSuccessful_ReturnsDecodedPayload ()
    {
        var response = CreateResponse(
            status: IpcProtocol.StatusOk,
            errors: Array.Empty<IpcError>(),
            payload: new IpcDaemonLogsReadResponse(
                Events:
                [
                    new IpcDaemonLogEvent(
                        Timestamp: "2026-03-05T10:30:00+09:00",
                        Level: "info",
                        Category: "ipc",
                        Message: "Server started.",
                        Raw: null,
                        Cursor: "stream-1:10"),
                ],
                NextCursor: "stream-1:11"));

        var result = IpcDaemonLogsResponseCodec.TryDecode(response, out var payload, out var error);

        Assert.True(result);
        Assert.Null(error);
        var decodedPayload = Assert.IsType<IpcDaemonLogsReadResponse>(payload);
        Assert.Single(decodedPayload.Events);
        Assert.Equal("stream-1:11", decodedPayload.NextCursor);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryDecode_WhenResponseContainsInvalidArgumentError_ReturnsInvalidArgument ()
    {
        var response = CreateResponse(
            status: IpcProtocol.StatusError,
            errors:
            [
                new IpcError(IpcErrorCodes.InvalidArgument, "queryTarget stack is not supported.", null),
            ],
            payload: new { });

        var result = IpcDaemonLogsResponseCodec.TryDecode(response, out var payload, out var error);

        Assert.False(result);
        Assert.Null(payload);
        var executionError = Assert.IsType<ExecutionError>(error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, executionError.Kind);
        Assert.Contains("INVALID_ARGUMENT", executionError.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryDecode_WhenPayloadIsInvalid_ReturnsInternalError ()
    {
        var response = CreateResponse(
            status: IpcProtocol.StatusOk,
            errors: Array.Empty<IpcError>(),
            payload: new
            {
                events = Array.Empty<object>(),
                nextCursor = "",
            });

        var result = IpcDaemonLogsResponseCodec.TryDecode(response, out var payload, out var error);

        Assert.False(result);
        Assert.Null(payload);
        var executionError = Assert.IsType<ExecutionError>(error);
        Assert.Equal(ExecutionErrorKind.InternalError, executionError.Kind);
        Assert.Contains("nextCursor", executionError.Message, StringComparison.Ordinal);
    }

    private static IpcResponse CreateResponse (
        string status,
        IReadOnlyList<IpcError> errors,
        object payload)
    {
        return new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: "req-daemon-logs",
            Status: status,
            Payload: IpcPayloadCodec.SerializeToElement(payload),
            Errors: errors);
    }
}
