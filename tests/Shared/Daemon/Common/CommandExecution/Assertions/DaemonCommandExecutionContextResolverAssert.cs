namespace MackySoft.Ucli.TestSupport;

internal static class DaemonCommandExecutionContextResolverAssert
{
    public static RecordingDaemonCommandExecutionContextResolver.Invocation ResolvedFor (
        RecordingDaemonCommandExecutionContextResolver resolver,
        UcliCommand expectedTimeoutCommand,
        string? expectedProjectPath,
        int? expectedTimeoutMilliseconds,
        CancellationToken expectedCancellationToken = default)
    {
        var invocation = Assert.Single(resolver.Invocations);
        Assert.Equal(expectedTimeoutCommand, invocation.TimeoutCommand);
        Assert.Equal(expectedProjectPath, invocation.ProjectPath);
        Assert.Equal(expectedTimeoutMilliseconds, invocation.TimeoutMilliseconds);
        Assert.Equal(expectedCancellationToken, invocation.CancellationToken);
        return invocation;
    }
}
