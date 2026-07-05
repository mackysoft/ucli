namespace MackySoft.Ucli.Application.Tests;

internal static class DaemonTestRunClientAssert
{
    public static void ExecutionRequested (
        RecordingDaemonTestRunClient daemonTestRunClient,
        bool expectedFailFast)
    {
        var invocation = Assert.Single(daemonTestRunClient.Invocations);
        Assert.Equal(expectedFailFast, invocation.FailFast);
    }
}
