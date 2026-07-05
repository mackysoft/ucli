using MackySoft.Ucli.Contracts.Testing;

namespace MackySoft.Ucli.Application.Tests;

internal static class VerifyStepInvocationAssert
{
    public static RecordingVerifyCompileService.Invocation CompileRequestedWithTimeout (
        RecordingVerifyCompileService compileService,
        int expectedTimeoutMilliseconds,
        bool? expectProgressSink = null)
    {
        var invocation = Assert.Single(compileService.Invocations);
        Assert.Equal(expectedTimeoutMilliseconds, invocation.Input.TimeoutMilliseconds);
        if (expectProgressSink.HasValue)
        {
            if (expectProgressSink.Value)
            {
                Assert.NotNull(invocation.ProgressSink);
            }
            else
            {
                Assert.Null(invocation.ProgressSink);
            }
        }

        return invocation;
    }

    public static RecordingVerifyTestRunService.Invocation TestRunRequestedWithPlatform (
        RecordingVerifyTestRunService testRunService,
        TestRunPlatform expectedPlatform)
    {
        var invocation = Assert.Single(testRunService.Invocations);
        Assert.Equal(expectedPlatform, invocation.Input.TestPlatform);
        return invocation;
    }
}
