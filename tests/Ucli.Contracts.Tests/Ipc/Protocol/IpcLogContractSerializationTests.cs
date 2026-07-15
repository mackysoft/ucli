using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcLogContractSerializationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void IpcDaemonLogsContracts_SerializeWithCamelCaseFields ()
    {
        var requestPayload = new IpcDaemonLogsReadRequest(
            Tail: 120,
            After: "stream-1:10",
            Since: "2026-03-05T10:30:00+09:00",
            Until: "2026-03-05T10:40:00+09:00",
            Level: IpcLogLevel.Warning,
            Query: "connection",
            QueryTarget: IpcLogQueryTarget.Message,
            Category: "transport");
        var responsePayload = new IpcDaemonLogsReadResponse(
            Events:
            [
                new IpcDaemonLogEvent(
                    Timestamp: new DateTimeOffset(2026, 3, 5, 10, 35, 22, TimeSpan.FromHours(9)),
                    Level: IpcLogLevel.Warning,
                    Category: "transport",
                    Message: "Named pipe listener ignored recoverable connection error.",
                    Raw: "IOException: broken pipe",
                    Cursor: "stream-1:42"),
            ],
            NextCursor: "stream-1:43");

        var request = IpcPayloadCodec.SerializeToElement(requestPayload);
        var response = IpcPayloadCodec.SerializeToElement(responsePayload);

        JsonAssert.For(request)
            .HasInt32("tail", 120)
            .HasString("after", "stream-1:10")
            .HasString("since", "2026-03-05T10:30:00+09:00")
            .HasString("until", "2026-03-05T10:40:00+09:00")
            .HasString("level", "warning")
            .HasString("query", "connection")
            .HasString("queryTarget", "message")
            .HasString("category", "transport");
        JsonAssert.For(response)
            .HasArrayLength("events", 1)
            .HasString("nextCursor", "stream-1:43")
            .HasProperty("events", 0, eventObject => eventObject
                .HasString("timestamp", "2026-03-05T10:35:22+09:00")
                .HasString("level", "warning")
                .HasString("category", "transport")
                .HasString("message", "Named pipe listener ignored recoverable connection error.")
                .HasString("raw", "IOException: broken pipe")
                .HasString("cursor", "stream-1:42"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcUnityLogsContracts_SerializeWithCamelCaseFields ()
    {
        var requestPayload = new IpcUnityLogsReadRequest(
            Tail: 50,
            After: "stream-1:10",
            Since: "2026-03-05T10:30:00+09:00",
            Until: "2026-03-05T10:40:00+09:00",
            Level: IpcLogLevel.Warning,
            Query: "socket",
            QueryTarget: IpcLogQueryTarget.Stack,
            Source: IpcUnityLogSource.Runtime,
            StackTrace: IpcUnityLogStackTraceMode.Error,
            StackTraceMaxFrames: 10,
            StackTraceMaxChars: 2048);
        var responsePayload = new IpcUnityLogsReadResponse(
            Events:
            [
                new IpcUnityLogEvent(
                    Timestamp: new DateTimeOffset(2026, 3, 5, 10, 35, 22, TimeSpan.FromHours(9)),
                    Level: IpcLogLevel.Warning,
                    Source: IpcUnityLogSource.Runtime,
                    Message: "Socket timeout detected.",
                    StackTrace: "at Listener.Run()",
                    Cursor: "stream-1:42"),
            ],
            NextCursor: "stream-1:43");

        var request = IpcPayloadCodec.SerializeToElement(requestPayload);
        var response = IpcPayloadCodec.SerializeToElement(responsePayload);

        JsonAssert.For(request)
            .HasInt32("tail", 50)
            .HasString("after", "stream-1:10")
            .HasString("since", "2026-03-05T10:30:00+09:00")
            .HasString("until", "2026-03-05T10:40:00+09:00")
            .HasString("level", "warning")
            .HasString("query", "socket")
            .HasString("queryTarget", "stack")
            .HasString("source", "runtime")
            .HasString("stackTrace", "error")
            .HasInt32("stackTraceMaxFrames", 10)
            .HasInt32("stackTraceMaxChars", 2048);
        JsonAssert.For(response)
            .HasArrayLength("events", 1)
            .HasString("nextCursor", "stream-1:43")
            .HasProperty("events", 0, eventObject => eventObject
                .HasString("timestamp", "2026-03-05T10:35:22+09:00")
                .HasString("level", "warning")
                .HasString("source", "runtime")
                .HasString("message", "Socket timeout detected.")
                .HasString("stackTrace", "at Listener.Run()")
                .HasString("cursor", "stream-1:42"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcDaemonLogsReadResponse_WhenJsonEventTimestampHasNoTimezone_RejectsPayload ()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "events": [
                {
                  "timestamp": "2026-03-05T10:35:22",
                  "level": "info",
                  "category": "ipc",
                  "message": "message",
                  "raw": null,
                  "cursor": "stream-1:42"
                }
              ],
              "nextCursor": "stream-1:43"
            }
            """);

        var success = IpcPayloadCodec.TryDeserialize<IpcDaemonLogsReadResponse>(
            document.RootElement,
            out _,
            out var error);

        Assert.False(success);
        Assert.Equal(IpcPayloadReadErrorKind.DeserializeFailed, error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcUnityLogsReadResponse_WhenJsonEventTimestampIsMalformed_RejectsPayload ()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "events": [
                {
                  "timestamp": "not-a-timestamp",
                  "level": "info",
                  "source": "runtime",
                  "message": "message",
                  "stackTrace": null,
                  "cursor": "stream-1:42"
                }
              ],
              "nextCursor": "stream-1:43"
            }
            """);

        var success = IpcPayloadCodec.TryDeserialize<IpcUnityLogsReadResponse>(
            document.RootElement,
            out _,
            out var error);

        Assert.False(success);
        Assert.Equal(IpcPayloadReadErrorKind.DeserializeFailed, error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcUnityConsoleClearContracts_SerializeWithCamelCaseFields ()
    {
        var requestPayload = new IpcUnityConsoleClearRequest(UcliCommandIds.LogsUnityClear.Name);
        var responsePayload = new IpcUnityConsoleClearResponse();

        var request = IpcPayloadCodec.SerializeToElement(requestPayload);
        var response = IpcPayloadCodec.SerializeToElement(responsePayload);

        JsonAssert.For(request)
            .HasString("requestedBy", UcliCommandIds.LogsUnityClear.Name);
        JsonAssert.For(response)
            .HasValueKind(JsonValueKind.Object);
        Assert.Empty(response.EnumerateObject());
    }
}
