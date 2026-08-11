using MackySoft.Ucli.Application.Shared.EnvironmentVariables;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Tests;

public sealed class ProjectPathInputResolverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WithCommandOptionValue_PrefersCommandOption ()
    {
        var resolver = new ProjectPathInputResolver(new StubEnvironmentVariableReader(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [UcliEnvironmentVariableNames.ProjectPath] = "./env-project",
        }));

        var result = resolver.Resolve(new ProjectContextResolutionInput(
            CommandOptionProjectPath: Resolve("./cli-project"),
            FallbackProjectPath: Resolve("./fallback-project"),
            FallbackSourceLabel: "fallback.source"));

        Assert.True(result.IsSuccess);
        var candidate = Assert.IsType<ProjectPathCandidate>(result.Candidate);
        Assert.True(candidate.Path.IsSameAs(Resolve("./cli-project")));
        Assert.Equal(UnityProjectPathSource.CommandOption, candidate.Source);
        Assert.Equal("--projectPath", candidate.SourceLabel);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WhenCommandOptionIsMissing_UsesEnvironmentVariable ()
    {
        var resolver = new ProjectPathInputResolver(new StubEnvironmentVariableReader(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [UcliEnvironmentVariableNames.ProjectPath] = "./env-project",
        }));

        var result = resolver.Resolve(new ProjectContextResolutionInput(
            CommandOptionProjectPath: null,
            FallbackProjectPath: Resolve("./fallback-project"),
            FallbackSourceLabel: "fallback.source"));

        Assert.True(result.IsSuccess);
        var candidate = Assert.IsType<ProjectPathCandidate>(result.Candidate);
        Assert.True(candidate.Path.IsSameAs(Resolve("./env-project")));
        Assert.Equal(UnityProjectPathSource.EnvironmentVariable, candidate.Source);
        Assert.Equal(UcliEnvironmentVariableNames.ProjectPath, candidate.SourceLabel);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WhenEnvironmentVariableIsInvalid_ReturnsInvalidFormat ()
    {
        var resolver = new ProjectPathInputResolver(new StubEnvironmentVariableReader(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [UcliEnvironmentVariableNames.ProjectPath] = "invalid\0path",
        }));

        var result = resolver.Resolve(new ProjectContextResolutionInput(
            CommandOptionProjectPath: null,
            FallbackProjectPath: Resolve("./fallback-project"),
            FallbackSourceLabel: "testRunProfile.projectPath"));

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ProjectContextErrorCodes.ProjectPathInvalidFormat, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WhenInputsAreMissing_UsesCurrentDirectory ()
    {
        var resolver = new ProjectPathInputResolver(new StubEnvironmentVariableReader());

        var result = resolver.Resolve(new ProjectContextResolutionInput(
            CommandOptionProjectPath: null,
            FallbackProjectPath: null));

        Assert.True(result.IsSuccess);
        var candidate = Assert.IsType<ProjectPathCandidate>(result.Candidate);
        Assert.True(candidate.Path.IsSameAs(AbsolutePath.Parse(Environment.CurrentDirectory)));
        Assert.Equal(UnityProjectPathSource.CurrentDirectory, candidate.Source);
        Assert.Null(candidate.SourceLabel);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WhenEnvironmentIsMissing_UsesFallback ()
    {
        var resolver = new ProjectPathInputResolver(new StubEnvironmentVariableReader());

        var result = resolver.Resolve(new ProjectContextResolutionInput(
            CommandOptionProjectPath: null,
            FallbackProjectPath: Resolve("./fallback-project"),
            FallbackSourceLabel: "fallback.source"));

        Assert.True(result.IsSuccess);
        var candidate = Assert.IsType<ProjectPathCandidate>(result.Candidate);
        Assert.True(candidate.Path.IsSameAs(Resolve("./fallback-project")));
        Assert.Equal(UnityProjectPathSource.Fallback, candidate.Source);
    }

    private static AbsolutePath Resolve (string path)
    {
        var result = ProjectPathNormalizer.Normalize(path, "test");
        Assert.True(result.IsSuccess);
        return result.ProjectPath;
    }
}
