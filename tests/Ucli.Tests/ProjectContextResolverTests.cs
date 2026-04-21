namespace MackySoft.Ucli.Tests;

using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Shared.Configuration;
using MackySoft.Ucli.Shared.Context;
using MackySoft.Ucli.Shared.EnvironmentVariables;
using MackySoft.Ucli.Shared.Foundation;
using MackySoft.Ucli.UnityIntegration.Indexing.ReadIndex;
using MackySoft.Ucli.Shared.Context.Project;
using MackySoft.Ucli.UnityIntegration.Project.Resolution;

public sealed class ProjectContextResolverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_ReturnsContextWithDefaultConfig_WhenConfigFileDoesNotExist ()
    {
        using var scope = TestDirectories.CreateTempScope("init-status-context-resolver", "default-config");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var resolver = CreateResolver();

        var result = await resolver.Resolve(unityProjectPath, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        var context = Assert.IsType<ProjectContext>(result.Context);
        Assert.Equal(unityProjectPath, context.UnityProject.UnityProjectRoot);
        Assert.Equal(unityProjectPath, context.UnityProject.RepositoryRoot);
        Assert.Equal(ConfigSource.Default, context.ConfigSource);
        Assert.Equal(OperationPolicy.Safe, context.Config.OperationPolicy);
        Assert.Equal(PlanTokenMode.Optional, context.Config.PlanTokenMode);
        Assert.Equal(ReadIndexMode.RequireFresh, context.Config.ReadIndexDefaultMode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_ReturnsContextWithFileConfig_WhenConfigFileExists ()
    {
        using var scope = TestDirectories.CreateTempScope("init-status-context-resolver", "file-config");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var configStore = new UcliConfigStore();
        var saveResult = await configStore.Save(
            unityProjectPath,
            new UcliConfig(
                SchemaVersion: 1,
                OperationPolicy: OperationPolicy.Advanced,
                PlanTokenMode: PlanTokenMode.Required,
                ReadIndexDefaultMode: ReadIndexMode.Disabled,
                OperationAllowlist:
                [
                    "^ucli\\.",
                    "^extension\\.",
                ]),
            CancellationToken.None);
        Assert.True(saveResult.IsSuccess);

        var resolver = new ProjectContextResolver(new UnityProjectResolver(), configStore);

        var result = await resolver.Resolve(unityProjectPath, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var context = Assert.IsType<ProjectContext>(result.Context);
        Assert.Equal(ConfigSource.File, context.ConfigSource);
        Assert.Equal(OperationPolicy.Advanced, context.Config.OperationPolicy);
        Assert.Equal(PlanTokenMode.Required, context.Config.PlanTokenMode);
        Assert.Equal(ReadIndexMode.Disabled, context.Config.ReadIndexDefaultMode);
        Assert.Equal(new[] { "^ucli\\.", "^extension\\." }, context.Config.OperationAllowlist);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_ReturnsInvalidArgument_WhenUnityProjectIsInvalid ()
    {
        using var scope = TestDirectories.CreateTempScope("init-status-context-resolver", "invalid-unity-project");
        var invalidPath = scope.GetPath("MissingUnityProject");
        var resolver = CreateResolver();

        var result = await resolver.Resolve(invalidPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Context);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_ReturnsInvalidArgument_WhenConfigFileIsInvalid ()
    {
        using var scope = TestDirectories.CreateTempScope("init-status-context-resolver", "invalid-config");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var configStore = new UcliConfigStore();
        var configPath = configStore.GetConfigPath(unityProjectPath);
        var relativeConfigPath = Path.GetRelativePath(scope.FullPath, configPath);
        scope.WriteFile(relativeConfigPath, "{");
        var resolver = new ProjectContextResolver(new UnityProjectResolver(), configStore);

        var result = await resolver.Resolve(unityProjectPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Context);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WithNullProjectPath_UsesEnvironmentVariableProjectPath ()
    {
        using var scope = TestDirectories.CreateTempScope("init-status-context-resolver", "environment-variable-project-path");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var resolver = CreateResolver(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [UcliEnvironmentVariableNames.ProjectPath] = unityProjectPath,
        });

        var result = await resolver.Resolve(null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var context = Assert.IsType<ProjectContext>(result.Context);
        Assert.Equal(unityProjectPath, context.UnityProject.UnityProjectRoot);
    }

    private static ProjectContextResolver CreateResolver (IReadOnlyDictionary<string, string?>? environmentVariables = null)
    {
        return new ProjectContextResolver(
            new UnityProjectResolver(new ProjectPathInputResolver(new StubEnvironmentVariableReader(
                environmentVariables ?? new Dictionary<string, string?>(StringComparer.Ordinal)))),
            new UcliConfigStore());
    }
}
