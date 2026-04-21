namespace MackySoft.Ucli.Tests;

using MackySoft.Ucli.Shared.EnvironmentVariables;
using MackySoft.Ucli.Shared.Context.Project;
using MackySoft.Ucli.UnityIntegration.Project.Resolution;

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

        var result = resolver.Resolve("./cli-project", "./fallback-project");

        Assert.Equal("./cli-project", result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WhenCommandOptionIsMissing_UsesEnvironmentVariable ()
    {
        var resolver = new ProjectPathInputResolver(new StubEnvironmentVariableReader(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [UcliEnvironmentVariableNames.ProjectPath] = "./env-project",
        }));

        var result = resolver.Resolve(null, "./fallback-project");

        Assert.Equal("./env-project", result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WhenEnvironmentVariableIsWhitespace_UsesFallback ()
    {
        var resolver = new ProjectPathInputResolver(new StubEnvironmentVariableReader(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [UcliEnvironmentVariableNames.ProjectPath] = "   ",
        }));

        var result = resolver.Resolve(null, "./fallback-project");

        Assert.Equal("./fallback-project", result);
    }
}
