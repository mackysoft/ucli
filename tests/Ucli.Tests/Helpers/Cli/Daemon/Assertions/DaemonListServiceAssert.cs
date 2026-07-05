namespace MackySoft.Tests;

internal static class DaemonListServiceAssert
{
    public static void ListRequested (
        StubDaemonListService service,
        string? expectedProjectPath,
        int? expectedTimeoutMilliseconds)
    {
        var invocation = Assert.Single(service.Invocations);
        Assert.Equal(expectedProjectPath, invocation.ProjectPath);
        Assert.Equal(expectedTimeoutMilliseconds, invocation.TimeoutMilliseconds);
    }
}
