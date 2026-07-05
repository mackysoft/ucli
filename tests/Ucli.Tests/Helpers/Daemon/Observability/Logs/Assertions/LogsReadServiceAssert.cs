namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal static class LogsReadServiceAssert
{
    public static void ReadRequestedWithTimeout (
        RecordingLogsDaemonService service,
        int expectedTimeoutMilliseconds)
    {
        Assert.Equal(expectedTimeoutMilliseconds, ReadRequestedOnce(service).Request.TimeoutMilliseconds);
    }

    public static void ReadRequestedWithTimeout (
        RecordingLogsUnityService service,
        int expectedTimeoutMilliseconds)
    {
        Assert.Equal(expectedTimeoutMilliseconds, ReadRequestedOnce(service).Request.TimeoutMilliseconds);
    }

    private static RecordingLogsDaemonService.Invocation ReadRequestedOnce (RecordingLogsDaemonService service)
    {
        return Assert.Single(service.Invocations);
    }

    private static RecordingLogsUnityService.Invocation ReadRequestedOnce (RecordingLogsUnityService service)
    {
        return Assert.Single(service.Invocations);
    }
}
