using MackySoft.FileSystem;
using MackySoft.Ucli.Tests.Helpers.Process;

namespace MackySoft.Ucli.Tests.Helpers.Git;

internal static class GitCommandProcessAssert
{
    public static void WorktreeRootRequestedWithTimeouts (
        RecordingProcessRunner processRunner,
        AbsolutePath expectedUnityProjectPath,
        params TimeSpan[] expectedTimeouts)
    {
        Assert.Collection(
            processRunner.Invocations,
            expectedTimeouts
                .Select<TimeSpan, Action<RecordingProcessRunner.Invocation>>(expectedTimeout =>
                    invocation => AssertWorktreeRootRequest(
                        invocation.Request,
                        expectedUnityProjectPath,
                        expectedTimeout))
                .ToArray());
    }

    private static void AssertWorktreeRootRequest (
        ProcessRunRequest request,
        AbsolutePath expectedUnityProjectPath,
        TimeSpan expectedTimeout)
    {
        Assert.Equal(
            ["-C", expectedUnityProjectPath.Value, "rev-parse", "--show-toplevel"],
            request.Arguments);
        Assert.Equal(expectedTimeout, request.Timeout);
    }
}
