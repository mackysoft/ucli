namespace MackySoft.Ucli.Application.Tests;

internal static class UnityProjectResolverAssert
{
    public static RecordingUnityProjectResolver.Invocation ResolvedOnceFor (
        RecordingUnityProjectResolver resolver,
        string expectedProjectPath,
        UnityProjectPathSource expectedSource)
    {
        var invocation = Assert.Single(resolver.Invocations);
        Assert.Equal(Path.GetFullPath(expectedProjectPath), Path.GetFullPath(invocation.ProjectPathCandidate.Path));
        Assert.Equal(expectedSource, invocation.ProjectPathCandidate.Source);
        return invocation;
    }
}
