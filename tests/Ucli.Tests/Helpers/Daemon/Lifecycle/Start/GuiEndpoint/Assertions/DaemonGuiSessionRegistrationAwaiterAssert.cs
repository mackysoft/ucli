namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal static class DaemonGuiSessionRegistrationAwaiterAssert
{
    public static RecordingDaemonGuiSessionRegistrationAwaiter.Invocation RegistrationWaitRequestedFor (
        RecordingDaemonGuiSessionRegistrationAwaiter awaiter,
        int expectedProcessId,
        DateTimeOffset? expectedProcessStartedAtUtc,
        TimeSpan? expectedTimeout = null)
    {
        var invocation = Assert.Single(awaiter.Invocations);
        Assert.Equal(expectedProcessId, invocation.ExpectedProcessId);
        Assert.Equal(expectedProcessStartedAtUtc, invocation.ExpectedProcessStartedAtUtc);
        if (expectedTimeout.HasValue)
        {
            Assert.Equal(expectedTimeout.Value, invocation.Timeout);
        }

        return invocation;
    }

    public static RecordingDaemonGuiSessionRegistrationAwaiter.Invocation RegistrationWaitRequestedWithTimeout (
        RecordingDaemonGuiSessionRegistrationAwaiter awaiter,
        TimeSpan expectedTimeout)
    {
        var invocation = Assert.Single(awaiter.Invocations);
        Assert.Equal(expectedTimeout, invocation.Timeout);
        return invocation;
    }
}
