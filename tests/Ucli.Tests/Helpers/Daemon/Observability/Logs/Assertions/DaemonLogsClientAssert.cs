using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal static class DaemonLogsClientAssert
{
    public static void ReadAfterCursors (
        RecordingDaemonLogsClient logsClient,
        params string?[] expectedAfterCursors)
    {
        Assert.Equal(expectedAfterCursors, logsClient.Invocations.Select(static invocation => invocation.Query.After).ToArray());
    }

    public static void SingleReadWithoutAfterCursor (RecordingDaemonLogsClient logsClient)
    {
        Assert.Null(SingleReadQuery(logsClient).After);
    }

    public static void ReadTailValues (
        RecordingDaemonLogsClient logsClient,
        params int?[] expectedTailValues)
    {
        Assert.Equal(expectedTailValues, logsClient.Invocations.Select(static invocation => invocation.Query.Tail).ToArray());
    }

    private static IpcDaemonLogsReadRequest SingleReadQuery (RecordingDaemonLogsClient logsClient)
    {
        return Assert.Single(logsClient.Invocations).Query;
    }
}
