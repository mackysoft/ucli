using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal static class UnityLogsClientAssert
{
    public static void ReadTimeouts (
        RecordingUnityLogsClient logsClient,
        params TimeSpan[] expectedTimeouts)
    {
        Assert.Equal(expectedTimeouts, logsClient.Invocations.Select(static invocation => invocation.Timeout).ToArray());
    }

    public static void ReadAfterCursors (
        RecordingUnityLogsClient logsClient,
        params string?[] expectedAfterCursors)
    {
        Assert.Equal(expectedAfterCursors, logsClient.Invocations.Select(static invocation => invocation.Query.After).ToArray());
    }

    public static void SingleReadWithoutAfterCursor (RecordingUnityLogsClient logsClient)
    {
        Assert.Null(SingleReadQuery(logsClient).After);
    }

    public static void SingleReadWithStackTraceNoneAndNoLimits (RecordingUnityLogsClient logsClient)
    {
        var query = SingleReadQuery(logsClient);
        Assert.Equal(IpcUnityLogStackTraceMode.None, query.StackTrace);
        Assert.Null(query.StackTraceMaxFrames);
        Assert.Null(query.StackTraceMaxChars);
    }

    public static void ReadTailValues (
        RecordingUnityLogsClient logsClient,
        params int?[] expectedTailValues)
    {
        Assert.Equal(expectedTailValues, logsClient.Invocations.Select(static invocation => invocation.Query.Tail).ToArray());
    }

    private static IpcUnityLogsReadRequest SingleReadQuery (RecordingUnityLogsClient logsClient)
    {
        return Assert.Single(logsClient.Invocations).Query;
    }
}
