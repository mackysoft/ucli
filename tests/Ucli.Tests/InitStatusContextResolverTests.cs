namespace MackySoft.Ucli.Tests;

using MackySoft.Tests;
using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Context;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.UnityProject;

public sealed class InitStatusContextResolverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_ReturnsContextWithDefaultConfig_WhenConfigFileDoesNotExist ()
    {
        using var scope = TestDirectories.CreateTempScope("init-status-context-resolver", "default-config");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var resolver = CreateResolver();

        var result = resolver.Resolve(unityProjectPath);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        var context = Assert.IsType<InitStatusContext>(result.Context);
        Assert.Equal(unityProjectPath, context.UnityProject.UnityProjectRoot);
        Assert.Equal(ConfigSource.Default, context.ConfigSource);
        Assert.Equal(OperationPolicy.Safe, context.Config.OperationPolicy);
        Assert.Equal(PlanTokenMode.Optional, context.Config.PlanTokenMode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_ReturnsContextWithFileConfig_WhenConfigFileExists ()
    {
        using var scope = TestDirectories.CreateTempScope("init-status-context-resolver", "file-config");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var configStore = new UcliConfigStore();
        var saveResult = configStore.Save(
            unityProjectPath,
            new UcliConfig(
                SchemaVersion: 1,
                OperationPolicy: OperationPolicy.Advanced,
                PlanTokenMode: PlanTokenMode.Required,
                OperationAllowlist:
                [
                    "^ucli\\.",
                    "^extension\\.",
                ]));
        Assert.True(saveResult.IsSuccess);

        var resolver = new InitStatusContextResolver(new UnityProjectResolver(), configStore);

        var result = resolver.Resolve(unityProjectPath);

        Assert.True(result.IsSuccess);
        var context = Assert.IsType<InitStatusContext>(result.Context);
        Assert.Equal(ConfigSource.File, context.ConfigSource);
        Assert.Equal(OperationPolicy.Advanced, context.Config.OperationPolicy);
        Assert.Equal(PlanTokenMode.Required, context.Config.PlanTokenMode);
        Assert.Equal(new[] { "^ucli\\.", "^extension\\." }, context.Config.OperationAllowlist);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_ReturnsInvalidArgument_WhenUnityProjectIsInvalid ()
    {
        using var scope = TestDirectories.CreateTempScope("init-status-context-resolver", "invalid-unity-project");
        var invalidPath = scope.GetPath("MissingUnityProject");
        var resolver = CreateResolver();

        var result = resolver.Resolve(invalidPath);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Context);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_ReturnsInvalidArgument_WhenConfigFileIsInvalid ()
    {
        using var scope = TestDirectories.CreateTempScope("init-status-context-resolver", "invalid-config");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var configStore = new UcliConfigStore();
        var configPath = configStore.GetConfigPath(unityProjectPath);
        var relativeConfigPath = Path.GetRelativePath(scope.FullPath, configPath);
        scope.WriteFile(relativeConfigPath, "{");
        var resolver = new InitStatusContextResolver(new UnityProjectResolver(), configStore);

        var result = resolver.Resolve(unityProjectPath);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Context);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
    }

    private static InitStatusContextResolver CreateResolver ()
    {
        return new InitStatusContextResolver(
            new UnityProjectResolver(),
            new UcliConfigStore());
    }
}
