using MackySoft.Ucli.Tests.Helpers.Process;

namespace MackySoft.Ucli.Tests.Helpers.Git;

internal static class GitCommandProcessAssert
{
    public static void WorktreeRootRequestedWithTimeouts (
        RecordingProcessRunner processRunner,
        params TimeSpan[] expectedTimeouts)
    {
        Assert.Collection(
            processRunner.Invocations,
            expectedTimeouts
                .Select<TimeSpan, Action<RecordingProcessRunner.Invocation>>(expectedTimeout => invocation => AssertWorktreeRootRequest(invocation.Request, expectedTimeout))
                .ToArray());
    }

    private static void AssertWorktreeRootRequest (
        ProcessRunRequest request,
        TimeSpan expectedTimeout)
    {
        Assert.Equal(["-C", "/repo/wt-current/UnityProject", "rev-parse", "--show-toplevel"], request.Arguments);
        Assert.Equal(expectedTimeout, request.Timeout);
    }
}
