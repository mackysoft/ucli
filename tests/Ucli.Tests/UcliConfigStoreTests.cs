namespace MackySoft.Ucli.Tests;

using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.ReadIndex;

public sealed class UcliConfigStoreTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Load_ReturnsDefaultConfig_WhenConfigFileDoesNotExist ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-config-store", "load-default");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var configStore = new UcliConfigStore();

        var result = await configStore.Load(unityProjectPath, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        Assert.Equal(ConfigSource.Default, result.Source);
        var config = Assert.IsType<UcliConfig>(result.Config);
        Assert.Equal(UcliContractConstants.Config.SchemaVersion, config.SchemaVersion);
        Assert.Equal(OperationPolicy.Safe, config.OperationPolicy);
        Assert.Equal(PlanTokenMode.Optional, config.PlanTokenMode);
        Assert.Equal(ReadIndexMode.RequireFresh, config.ReadIndexDefaultMode);
        Assert.Equal(new[] { UcliContractConstants.Config.DefaultOperationAllowlistPattern }, config.OperationAllowlist);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Save_ThenLoad_RoundTripsConfigValues ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-config-store", "save-round-trip");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var configStore = new UcliConfigStore();
        var config = new UcliConfig(
            SchemaVersion: 1,
            OperationPolicy: OperationPolicy.Dangerous,
            PlanTokenMode: PlanTokenMode.Required,
            ReadIndexDefaultMode: ReadIndexMode.AllowStale,
            OperationAllowlist:
            [
                UcliContractConstants.Config.DefaultOperationAllowlistPattern,
                "^mylab\\.",
            ]);

        var saveResult = await configStore.Save(unityProjectPath, config, CancellationToken.None);

        Assert.True(saveResult.IsSuccess);
        Assert.Null(saveResult.Error);

        var loadResult = await configStore.Load(unityProjectPath, CancellationToken.None);
        Assert.True(loadResult.IsSuccess);
        Assert.Equal(ConfigSource.File, loadResult.Source);
        var loadedConfig = Assert.IsType<UcliConfig>(loadResult.Config);
        Assert.Equal(config.SchemaVersion, loadedConfig.SchemaVersion);
        Assert.Equal(config.OperationPolicy, loadedConfig.OperationPolicy);
        Assert.Equal(config.PlanTokenMode, loadedConfig.PlanTokenMode);
        Assert.Equal(config.ReadIndexDefaultMode, loadedConfig.ReadIndexDefaultMode);
        Assert.Equal(config.OperationAllowlist, loadedConfig.OperationAllowlist);

        var configPath = configStore.GetConfigPath(unityProjectPath);
        using var jsonDocument = JsonDocument.Parse(File.ReadAllText(configPath));
        JsonAssert.For(jsonDocument.RootElement)
            .HasInt32("schemaVersion", UcliContractConstants.Config.SchemaVersion)
            .HasString("operationPolicy", UcliContractConstants.Config.OperationPolicyDangerous)
            .HasString("planTokenMode", UcliContractConstants.Config.PlanTokenModeRequired)
            .HasString("readIndexDefaultMode", UcliContractConstants.Config.ReadIndexModeAllowStale)
            .HasArrayLength("operationAllowlist", 2);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Load_ReturnsInvalidArgument_WhenConfigJsonIsMalformed ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-config-store", "malformed-json");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var configStore = new UcliConfigStore();
        var configPath = configStore.GetConfigPath(unityProjectPath);
        var relativeConfigPath = Path.GetRelativePath(scope.FullPath, configPath);
        scope.WriteFile(relativeConfigPath, "{");

        var result = await configStore.Load(unityProjectPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Config);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("invalid", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Load_ReturnsInvalidArgument_WhenSchemaVersionDoesNotMatch ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-config-store", "schema-version-mismatch");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var configStore = new UcliConfigStore();
        var configPath = configStore.GetConfigPath(unityProjectPath);
        var relativeConfigPath = Path.GetRelativePath(scope.FullPath, configPath);
        var invalidSchemaConfigJson = JsonSerializer.Serialize(
            new
            {
                schemaVersion = 2,
                operationPolicy = UcliContractConstants.Config.OperationPolicySafe,
                planTokenMode = UcliContractConstants.Config.PlanTokenModeOptional,
                readIndexDefaultMode = UcliContractConstants.Config.ReadIndexModeRequireFresh,
                operationAllowlist = new[] { UcliContractConstants.Config.DefaultOperationAllowlistPattern },
            });
        scope.WriteFile(
            relativeConfigPath,
            invalidSchemaConfigJson);

        var result = await configStore.Load(unityProjectPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Config);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("schemaVersion", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Load_ReturnsInvalidArgument_WhenOperationAllowlistPatternIsInvalid ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-config-store", "invalid-allowlist-load");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var configStore = new UcliConfigStore();
        var configPath = configStore.GetConfigPath(unityProjectPath);
        var relativeConfigPath = Path.GetRelativePath(scope.FullPath, configPath);
        var invalidAllowlistConfigJson = JsonSerializer.Serialize(
            new
            {
                schemaVersion = UcliContractConstants.Config.SchemaVersion,
                operationPolicy = UcliContractConstants.Config.OperationPolicySafe,
                planTokenMode = UcliContractConstants.Config.PlanTokenModeOptional,
                readIndexDefaultMode = UcliContractConstants.Config.ReadIndexModeRequireFresh,
                operationAllowlist = new[] { "[" },
            });
        scope.WriteFile(relativeConfigPath, invalidAllowlistConfigJson);

        var result = await configStore.Load(unityProjectPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Config);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("operationAllowlist", error.Message, StringComparison.Ordinal);
        Assert.Contains("regex", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Save_ReturnsInvalidArgument_WhenOperationAllowlistPatternIsInvalid ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-config-store", "invalid-allowlist-save");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var configStore = new UcliConfigStore();
        var invalidConfig = new UcliConfig(
            SchemaVersion: UcliContractConstants.Config.SchemaVersion,
            OperationPolicy: OperationPolicy.Safe,
            PlanTokenMode: PlanTokenMode.Optional,
            ReadIndexDefaultMode: ReadIndexMode.RequireFresh,
            OperationAllowlist:
            [
                "[",
            ]);

        var saveResult = await configStore.Save(unityProjectPath, invalidConfig, CancellationToken.None);

        Assert.False(saveResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(saveResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("operationAllowlist", error.Message, StringComparison.Ordinal);
        Assert.Contains("regex", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Load_ReturnsInvalidArgument_WhenReadIndexDefaultModeIsInvalid ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-config-store", "invalid-read-index-default-mode");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var configStore = new UcliConfigStore();
        var configPath = configStore.GetConfigPath(unityProjectPath);
        var relativeConfigPath = Path.GetRelativePath(scope.FullPath, configPath);
        var invalidConfigJson = JsonSerializer.Serialize(
            new
            {
                schemaVersion = UcliContractConstants.Config.SchemaVersion,
                operationPolicy = UcliContractConstants.Config.OperationPolicySafe,
                planTokenMode = UcliContractConstants.Config.PlanTokenModeOptional,
                readIndexDefaultMode = "unknown",
                operationAllowlist = new[] { UcliContractConstants.Config.DefaultOperationAllowlistPattern },
            });
        scope.WriteFile(relativeConfigPath, invalidConfigJson);

        var result = await configStore.Load(unityProjectPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Config);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("readIndexDefaultMode", error.Message, StringComparison.Ordinal);
    }
}