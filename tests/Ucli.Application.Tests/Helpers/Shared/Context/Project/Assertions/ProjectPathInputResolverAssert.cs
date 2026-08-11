namespace MackySoft.Ucli.Application.Tests;

internal static class ProjectPathInputResolverAssert
{
    public static RecordingProjectPathInputResolver.Invocation ResolvedOnceFor (
        RecordingProjectPathInputResolver resolver,
        AbsolutePath? expectedCommandOptionProjectPath,
        AbsolutePath? expectedFallbackProjectPath,
        string? expectedFallbackSourceLabel,
        AbsolutePath expectedResolvedPath,
        UnityProjectPathSource expectedSource)
    {
        var invocation = Assert.Single(resolver.Invocations);
        Assert.Equal(expectedCommandOptionProjectPath, invocation.Input.CommandOptionProjectPath);
        Assert.Equal(expectedFallbackProjectPath, invocation.Input.FallbackProjectPath);
        Assert.Equal(expectedFallbackSourceLabel, invocation.Input.FallbackSourceLabel);
        Assert.True(invocation.Result.Candidate!.Path.IsSameAs(expectedResolvedPath));
        Assert.Equal(expectedSource, invocation.Result.Candidate.Source);
        return invocation;
    }
}
