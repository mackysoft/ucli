namespace MackySoft.Ucli.Application.Tests;

internal static class ProjectPathInputResolverAssert
{
    public static RecordingProjectPathInputResolver.Invocation ResolvedOnceFor (
        RecordingProjectPathInputResolver resolver,
        string? expectedCommandOptionProjectPath,
        string? expectedFallbackProjectPath,
        string? expectedFallbackSourceLabel,
        string expectedResolvedPath,
        UnityProjectPathSource expectedSource)
    {
        var invocation = Assert.Single(resolver.Invocations);
        Assert.Equal(expectedCommandOptionProjectPath, invocation.Input.CommandOptionProjectPath);
        Assert.Equal(expectedFallbackProjectPath, invocation.Input.FallbackProjectPath);
        Assert.Equal(expectedFallbackSourceLabel, invocation.Input.FallbackSourceLabel);
        Assert.Equal(Path.GetFullPath(expectedResolvedPath), Path.GetFullPath(invocation.ProjectPathCandidate.Path));
        Assert.Equal(expectedSource, invocation.ProjectPathCandidate.Source);
        return invocation;
    }
}
