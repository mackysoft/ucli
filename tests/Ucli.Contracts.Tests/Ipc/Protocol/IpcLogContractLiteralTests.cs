using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcLogContractLiteralTests
{
    public static TheoryData<IpcLogLevel, string> LogLevelCases => new()
    {
        { IpcLogLevel.Error, "error" },
        { IpcLogLevel.Warning, "warning" },
        { IpcLogLevel.Info, "info" },
    };

    public static TheoryData<IpcLogQueryTarget, string> QueryTargetCases => new()
    {
        { IpcLogQueryTarget.Message, "message" },
        { IpcLogQueryTarget.Stack, "stack" },
        { IpcLogQueryTarget.Both, "both" },
    };

    public static TheoryData<IpcUnityLogSource, string> UnityLogSourceCases => new()
    {
        { IpcUnityLogSource.Compile, "compile" },
        { IpcUnityLogSource.Runtime, "runtime" },
    };

    public static TheoryData<IpcUnityLogStackTraceMode, string> StackTraceModeCases => new()
    {
        { IpcUnityLogStackTraceMode.None, "none" },
        { IpcUnityLogStackTraceMode.Error, "error" },
        { IpcUnityLogStackTraceMode.All, "all" },
    };

    [Theory]
    [MemberData(nameof(LogLevelCases))]
    [Trait("Size", "Small")]
    public void IpcLogLevel_HasStableContractLiteral (IpcLogLevel value, string expectedLiteral)
    {
        Assert.Equal(expectedLiteral, ContractLiteralCodec.ToValue(value));
    }

    [Theory]
    [MemberData(nameof(QueryTargetCases))]
    [Trait("Size", "Small")]
    public void IpcLogQueryTarget_HasStableContractLiteral (IpcLogQueryTarget value, string expectedLiteral)
    {
        Assert.Equal(expectedLiteral, ContractLiteralCodec.ToValue(value));
    }

    [Theory]
    [MemberData(nameof(UnityLogSourceCases))]
    [Trait("Size", "Small")]
    public void IpcUnityLogSource_HasStableContractLiteral (IpcUnityLogSource value, string expectedLiteral)
    {
        Assert.Equal(expectedLiteral, ContractLiteralCodec.ToValue(value));
    }

    [Theory]
    [MemberData(nameof(StackTraceModeCases))]
    [Trait("Size", "Small")]
    public void IpcUnityLogStackTraceMode_HasStableContractLiteral (IpcUnityLogStackTraceMode value, string expectedLiteral)
    {
        Assert.Equal(expectedLiteral, ContractLiteralCodec.ToValue(value));
    }

    [Theory]
    [InlineData("all")]
    [InlineData("")]
    [Trait("Size", "Small")]
    public void IpcLogLevel_DoesNotRepresentFilterWildcard (string literal)
    {
        Assert.False(ContractLiteralCodec.TryParse(literal, out IpcLogLevel _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcUnityLogSource_DoesNotRepresentFilterWildcard ()
    {
        Assert.False(ContractLiteralCodec.TryParse("all", out IpcUnityLogSource _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcDaemonLogEvent_WhenLevelIsUndefined_Throws ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new IpcDaemonLogEvent(
            Timestamp: new DateTimeOffset(2026, 3, 5, 10, 35, 22, TimeSpan.Zero),
            Level: (IpcLogLevel)999,
            Category: "ipc",
            Message: "message",
            Raw: null,
            Cursor: "cursor"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcDaemonLogEvent_WhenTimestampIsDefault_Throws ()
    {
        Assert.Throws<ArgumentException>(() => new IpcDaemonLogEvent(
            Timestamp: default,
            Level: IpcLogLevel.Info,
            Category: "ipc",
            Message: "message",
            Raw: null,
            Cursor: "cursor"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcDaemonLogsReadRequest_WhenQueryTargetIsUndefined_Throws ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new IpcDaemonLogsReadRequest(
            Tail: null,
            After: null,
            Since: null,
            Until: null,
            Level: null,
            Query: null,
            QueryTarget: (IpcLogQueryTarget)999,
            Category: null));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcDaemonLogsReadRequest_WhenLevelIsUndefined_Throws ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new IpcDaemonLogsReadRequest(
            Tail: null,
            After: null,
            Since: null,
            Until: null,
            Level: (IpcLogLevel)999,
            Query: null,
            QueryTarget: null,
            Category: null));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcUnityLogEvent_WhenSourceIsUndefined_Throws ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new IpcUnityLogEvent(
            Timestamp: new DateTimeOffset(2026, 3, 5, 10, 35, 22, TimeSpan.Zero),
            Level: IpcLogLevel.Info,
            Source: (IpcUnityLogSource)999,
            Message: "message",
            StackTrace: null,
            Cursor: "cursor"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcUnityLogEvent_WhenTimestampIsDefault_Throws ()
    {
        Assert.Throws<ArgumentException>(() => new IpcUnityLogEvent(
            Timestamp: default,
            Level: IpcLogLevel.Info,
            Source: IpcUnityLogSource.Runtime,
            Message: "message",
            StackTrace: null,
            Cursor: "cursor"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcUnityLogsReadRequest_WhenSourceIsUndefined_Throws ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new IpcUnityLogsReadRequest(
            Tail: null,
            After: null,
            Since: null,
            Until: null,
            Level: null,
            Query: null,
            QueryTarget: null,
            Source: (IpcUnityLogSource)999,
            StackTrace: null,
            StackTraceMaxFrames: null,
            StackTraceMaxChars: null));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcUnityLogsReadRequest_WhenStackTraceModeIsUndefined_Throws ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new IpcUnityLogsReadRequest(
            Tail: null,
            After: null,
            Since: null,
            Until: null,
            Level: null,
            Query: null,
            QueryTarget: null,
            Source: null,
            StackTrace: (IpcUnityLogStackTraceMode)999,
            StackTraceMaxFrames: null,
            StackTraceMaxChars: null));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcDaemonLogsReadResponse_WhenEventsContainNull_Throws ()
    {
        Assert.Throws<ArgumentException>(() => new IpcDaemonLogsReadResponse(
            Events: new IpcDaemonLogEvent[] { null! },
            NextCursor: "cursor"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcUnityLogsReadResponse_WhenNextCursorIsEmpty_Throws ()
    {
        Assert.Throws<ArgumentException>(() => new IpcUnityLogsReadResponse(
            Events: Array.Empty<IpcUnityLogEvent>(),
            NextCursor: string.Empty));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcDaemonLogsReadResponse_WhenSourceEventsChange_PreservesSnapshot ()
    {
        var originalEvent = new IpcDaemonLogEvent(
            Timestamp: new DateTimeOffset(2026, 3, 5, 10, 35, 22, TimeSpan.Zero),
            Level: IpcLogLevel.Info,
            Category: "ipc",
            Message: "original",
            Raw: null,
            Cursor: "cursor-1");
        var sourceEvents = new List<IpcDaemonLogEvent> { originalEvent };
        var response = new IpcDaemonLogsReadResponse(sourceEvents, "cursor-2");

        sourceEvents[0] = new IpcDaemonLogEvent(
            Timestamp: new DateTimeOffset(2026, 3, 5, 10, 35, 23, TimeSpan.Zero),
            Level: IpcLogLevel.Warning,
            Category: "transport",
            Message: "replacement",
            Raw: null,
            Cursor: "cursor-2");
        sourceEvents.Add(originalEvent);

        Assert.Collection(response.Events, item => Assert.Same(originalEvent, item));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcUnityLogsReadResponse_WhenSourceEventsChange_PreservesSnapshot ()
    {
        var originalEvent = new IpcUnityLogEvent(
            Timestamp: new DateTimeOffset(2026, 3, 5, 10, 35, 22, TimeSpan.Zero),
            Level: IpcLogLevel.Info,
            Source: IpcUnityLogSource.Runtime,
            Message: "original",
            StackTrace: null,
            Cursor: "cursor-1");
        var sourceEvents = new List<IpcUnityLogEvent> { originalEvent };
        var response = new IpcUnityLogsReadResponse(sourceEvents, "cursor-2");

        sourceEvents[0] = new IpcUnityLogEvent(
            Timestamp: new DateTimeOffset(2026, 3, 5, 10, 35, 23, TimeSpan.Zero),
            Level: IpcLogLevel.Warning,
            Source: IpcUnityLogSource.Compile,
            Message: "replacement",
            StackTrace: null,
            Cursor: "cursor-2");
        sourceEvents.Add(originalEvent);

        Assert.Collection(response.Events, item => Assert.Same(originalEvent, item));
    }
}
