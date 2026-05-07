using MackySoft.Ucli.Application.Shared.EnvironmentVariables;

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
            CommandOptionProjectPath: "./cli-project",
            FallbackProjectPath: "./fallback-project",
            FallbackSourceLabel: "fallback.source"));

        Assert.Equal("./cli-project", result.Path);
        Assert.Equal(UnityProjectPathSource.CommandOption, result.Source);
        Assert.Equal("--projectPath", result.SourceLabel);
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
            FallbackProjectPath: "./fallback-project",
            FallbackSourceLabel: "fallback.source"));

        Assert.Equal("./env-project", result.Path);
        Assert.Equal(UnityProjectPathSource.EnvironmentVariable, result.Source);
        Assert.Equal(UcliEnvironmentVariableNames.ProjectPath, result.SourceLabel);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WhenEnvironmentVariableIsWhitespace_UsesFallback ()
    {
        var resolver = new ProjectPathInputResolver(new StubEnvironmentVariableReader(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [UcliEnvironmentVariableNames.ProjectPath] = "   ",
        }));

        var result = resolver.Resolve(new ProjectContextResolutionInput(
            CommandOptionProjectPath: null,
            FallbackProjectPath: "./fallback-project",
            FallbackSourceLabel: "testRunProfile.projectPath"));

        Assert.Equal("./fallback-project", result.Path);
        Assert.Equal(UnityProjectPathSource.Fallback, result.Source);
        Assert.Equal("testRunProfile.projectPath", result.SourceLabel);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WhenInputsAreMissing_UsesCurrentDirectory ()
    {
        var resolver = new ProjectPathInputResolver(new StubEnvironmentVariableReader());

        var result = resolver.Resolve(new ProjectContextResolutionInput(
            CommandOptionProjectPath: null,
            FallbackProjectPath: null));

        Assert.Equal(".", result.Path);
        Assert.Equal(UnityProjectPathSource.CurrentDirectory, result.Source);
        Assert.Null(result.SourceLabel);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WhenCommandAndEnvironmentAreWhitespace_UsesCurrentDirectory ()
    {
        var resolver = new ProjectPathInputResolver(new StubEnvironmentVariableReader(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [UcliEnvironmentVariableNames.ProjectPath] = "   ",
        }));

        var result = resolver.Resolve(new ProjectContextResolutionInput(
            CommandOptionProjectPath: "  ",
            FallbackProjectPath: "\t"));

        Assert.Equal(".", result.Path);
        Assert.Equal(UnityProjectPathSource.CurrentDirectory, result.Source);
    }

    private sealed class StubEnvironmentVariableReader : IEnvironmentVariableReader
    {
        private readonly IReadOnlyDictionary<string, string?> values;

        public StubEnvironmentVariableReader ()
            : this(new Dictionary<string, string?>(StringComparer.Ordinal))
        {
        }

        public StubEnvironmentVariableReader (IReadOnlyDictionary<string, string?> values)
        {
            this.values = values;
        }

        public string? Get (string variableName)
        {
            return values.TryGetValue(variableName, out var value) ? value : null;
        }
    }
}
