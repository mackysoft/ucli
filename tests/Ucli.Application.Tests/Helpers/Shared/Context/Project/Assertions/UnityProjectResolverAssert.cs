namespace MackySoft.Ucli.Application.Tests;

internal static class UnityProjectResolverAssert
{
    public static RecordingUnityProjectResolver.Invocation ResolvedOnceFor (
        RecordingUnityProjectResolver resolver,
        AbsolutePath expectedProjectPath,
        UnityProjectPathSource expectedSource)
    {
        var invocation = Assert.Single(resolver.Invocations);
        Assert.True(invocation.ProjectPathCandidate.Path.IsSameAs(expectedProjectPath));
        Assert.Equal(expectedSource, invocation.ProjectPathCandidate.Source);
        return invocation;
    }
}
