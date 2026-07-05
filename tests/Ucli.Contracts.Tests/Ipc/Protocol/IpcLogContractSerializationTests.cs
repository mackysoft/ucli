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
            Level: "warning",
            Query: "connection",
            QueryTarget: "message",
            Category: "transport");
        var responsePayload = new IpcDaemonLogsReadResponse(
            Events:
            [
                new IpcDaemonLogEvent(
                    Timestamp: "2026-03-05T10:35:22.0000000+09:00",
                    Level: "warning",
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
                .HasString("timestamp", "2026-03-05T10:35:22.0000000+09:00")
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
            Level: "warning",
            Query: "socket",
            QueryTarget: "stack",
            Source: "runtime",
            StackTrace: "error",
            StackTraceMaxFrames: 10,
            StackTraceMaxChars: 2048);
        var responsePayload = new IpcUnityLogsReadResponse(
            Events:
            [
                new IpcUnityLogEvent(
                    Timestamp: "2026-03-05T10:35:22.0000000+09:00",
                    Level: "warning",
                    Source: "runtime",
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
                .HasString("timestamp", "2026-03-05T10:35:22.0000000+09:00")
                .HasString("level", "warning")
                .HasString("source", "runtime")
                .HasString("message", "Socket timeout detected.")
                .HasString("stackTrace", "at Listener.Run()")
                .HasString("cursor", "stream-1:42"));
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
