using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Logs;

public sealed class IpcLogsResponseDecodeHelperTests
{
    private const string OperationName = "Logs read";

    [Fact]
    [Trait("Size", "Small")]
    public void TryDecodeReadPayload_WhenResponseIsSuccessful_ReturnsDecodedPayload ()
    {
        var response = CreateResponse(
            status: IpcResponseStatus.Ok,
            errors: [],
            payload: new IpcDaemonLogsReadResponse(
                Events:
                [
                    new IpcDaemonLogEvent(
                        Timestamp: new DateTimeOffset(2026, 3, 5, 10, 30, 0, TimeSpan.FromHours(9)),
                        Level: IpcLogLevel.Info,
                        Category: "ipc",
                        Message: "Server started.",
                        Raw: null,
                        Cursor: new IpcLogCursor("abcdef0123456789abcdef0123456789:10")),
                ],
                NextCursor: new IpcLogCursor("abcdef0123456789abcdef0123456789:11")));

        var result = IpcLogsResponseDecodeHelper.TryDecodeReadPayload<IpcDaemonLogsReadResponse>(
            response,
            OperationName,
            out var payload,
            out var error);

        Assert.True(result);
        Assert.Null(error);
        var decodedPayload = Assert.IsType<IpcDaemonLogsReadResponse>(payload);
        Assert.Single(decodedPayload.Events);
        Assert.Equal("abcdef0123456789abcdef0123456789:11", decodedPayload.NextCursor.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryDecodeReadPayload_WhenResponseContainsInvalidArgumentError_ReturnsInvalidArgument ()
    {
        var response = CreateResponse(
            status: IpcResponseStatus.Error,
            errors:
            [
                new IpcError(UcliCoreErrorCodes.InvalidArgument, "queryTarget stack is not supported.", null),
            ],
            payload: new { });

        var result = IpcLogsResponseDecodeHelper.TryDecodeReadPayload<IpcDaemonLogsReadResponse>(
            response,
            OperationName,
            out var payload,
            out var error);

        Assert.False(result);
        Assert.Null(payload);
        var executionError = Assert.IsType<ExecutionError>(error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, executionError.Kind);
        Assert.Contains(OperationName, executionError.Message, StringComparison.Ordinal);
        Assert.Contains("INVALID_ARGUMENT", executionError.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryDecodeReadPayload_WhenPayloadIsInvalid_ReturnsInternalError ()
    {
        var response = CreateResponse(
            status: IpcResponseStatus.Ok,
            errors: [],
            payload: new
            {
                events = Array.Empty<object>(),
                nextCursor = "",
            });

        var result = IpcLogsResponseDecodeHelper.TryDecodeReadPayload<IpcDaemonLogsReadResponse>(
            response,
            OperationName,
            out var payload,
            out var error);

        Assert.False(result);
        Assert.Null(payload);
        var executionError = Assert.IsType<ExecutionError>(error);
        Assert.Equal(ExecutionErrorKind.InternalError, executionError.Kind);
        Assert.Contains(OperationName, executionError.Message, StringComparison.Ordinal);
        Assert.Contains("payload is invalid", executionError.Message, StringComparison.Ordinal);
    }

    private static IpcResponse CreateResponse (
        IpcResponseStatus status,
        IReadOnlyList<IpcError> errors,
        object payload)
    {
        return new IpcResponse(
            protocolVersion: IpcProtocol.CurrentVersion,
            requestId: Guid.NewGuid(),
            status: status,
            payload: IpcPayloadCodec.SerializeToElement(payload),
            errors: errors);
    }
}
