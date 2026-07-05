namespace MackySoft.Ucli.Application.Tests;

internal static class ProjectContextResolverAssert
{
    public static StaticProjectContextResolver.Invocation ProjectContextResolvedOnce (
        StaticProjectContextResolver projectContextResolver,
        string? expectedProjectPath,
        CancellationToken expectedCancellationToken)
    {
        var invocation = Assert.Single(projectContextResolver.Invocations);
        Assert.Equal(expectedProjectPath, invocation.ProjectPath);
        Assert.Equal(expectedCancellationToken, invocation.CancellationToken);
        return invocation;
    }
}
