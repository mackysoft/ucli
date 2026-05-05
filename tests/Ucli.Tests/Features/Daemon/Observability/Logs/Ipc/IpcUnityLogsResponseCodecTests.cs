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
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Stop;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;

namespace MackySoft.Ucli.Tests.Logs;

public sealed class IpcUnityLogsResponseCodecTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryDecode_WhenResponseIsSuccessful_ReturnsDecodedPayload ()
    {
        var response = CreateResponse(
            status: IpcProtocol.StatusOk,
            errors: Array.Empty<IpcError>(),
            payload: new IpcUnityLogsReadResponse(
                Events:
                [
                    new IpcUnityLogEvent(
                        Timestamp: "2026-03-05T10:30:00+09:00",
                        Level: "info",
                        Source: "runtime",
                        Message: "Server started.",
                        StackTrace: null,
                        Cursor: "stream-1:10"),
                ],
                NextCursor: "stream-1:11"));

        var result = IpcUnityLogsResponseCodec.TryDecode(response, out var payload, out var error);

        Assert.True(result);
        Assert.Null(error);
        var decodedPayload = Assert.IsType<IpcUnityLogsReadResponse>(payload);
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
                new IpcError(IpcErrorCodes.InvalidArgument, "stackTrace is invalid.", null),
            ],
            payload: new { });

        var result = IpcUnityLogsResponseCodec.TryDecode(response, out var payload, out var error);

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

        var result = IpcUnityLogsResponseCodec.TryDecode(response, out var payload, out var error);

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
            RequestId: "req-unity-logs",
            Status: status,
            Payload: IpcPayloadCodec.SerializeToElement(payload),
            Errors: errors);
    }
}
