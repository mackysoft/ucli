namespace MackySoft.Ucli.Tests.Features.Daemon.Lifecycle.Process.Reachability;

public sealed class DaemonReachabilityClassifierTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void IsRequestTimeout_WhenDaemonReportsIpcTimeout_ReturnsTrue ()
    {
        var classifier = new DaemonReachabilityClassifier();

        var result = classifier.IsRequestTimeout(
            new DaemonPingResponseException(
                "The daemon timed out while handling the ping request.",
                IpcTransportErrorCodes.IpcTimeout));

        Assert.True(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsRequestTimeout_WhenDaemonReportsDifferentError_ReturnsFalse ()
    {
        var classifier = new DaemonReachabilityClassifier();

        var result = classifier.IsRequestTimeout(
            new DaemonPingResponseException(
                "The daemon rejected the ping request.",
                UcliCoreErrorCodes.InternalError));

        Assert.False(result);
    }
}
