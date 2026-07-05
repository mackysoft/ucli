using MackySoft.Ucli.Tests.Helpers.Ipc;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal static class SupervisorTransportAssert
{
    public static StubIpcTransportInvocation EnsureRunningRequestedWithUnboundedResponseWait (
        StubIpcTransportClient transportClient,
        TimeSpan? expectedTimeout = null)
    {
        var invocation = Assert.Single(
            transportClient.Invocations,
            static invocation => string.Equals(invocation.Request.Method, SupervisorIpcContracts.EnsureRunningMethod, StringComparison.Ordinal));
        Assert.True(invocation.UsesUnboundedResponseWait);
        if (expectedTimeout.HasValue)
        {
            Assert.Equal(expectedTimeout.Value, invocation.Timeout);
        }

        return invocation;
    }
}
