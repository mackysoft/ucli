namespace MackySoft.Ucli.Tests;

using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Contracts.Configuration;
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
        Assert.Equal(UcliContractConstants.Config.IpcDefaultTimeoutMilliseconds, config.IpcDefaultTimeoutMilliseconds);
        AssertDefaultIpcTimeouts(config.IpcTimeoutMillisecondsByCommand);
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
            ])
        {
            IpcDefaultTimeoutMilliseconds = 4500,
            IpcTimeoutMillisecondsByCommand = new Dictionary<string, int?>(StringComparer.Ordinal)
            {
                [UcliContractConstants.Config.IpcTimeoutCommandStatus] = null,
                [UcliContractConstants.Config.IpcTimeoutCommandCall] = 16000,
                [UcliContractConstants.Config.IpcTimeoutCommandPlan] = 8000,
            },
        };

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
        Assert.Equal(config.IpcDefaultTimeoutMilliseconds, loadedConfig.IpcDefaultTimeoutMilliseconds);
        Assert.Equal(config.IpcTimeoutMillisecondsByCommand.Count, loadedConfig.IpcTimeoutMillisecondsByCommand.Count);
        Assert.Equal(
            config.IpcTimeoutMillisecondsByCommand[UcliContractConstants.Config.IpcTimeoutCommandStatus],
            loadedConfig.IpcTimeoutMillisecondsByCommand[UcliContractConstants.Config.IpcTimeoutCommandStatus]);
        Assert.Equal(
            config.IpcTimeoutMillisecondsByCommand[UcliContractConstants.Config.IpcTimeoutCommandCall],
            loadedConfig.IpcTimeoutMillisecondsByCommand[UcliContractConstants.Config.IpcTimeoutCommandCall]);
        Assert.Equal(
            config.IpcTimeoutMillisecondsByCommand[UcliContractConstants.Config.IpcTimeoutCommandPlan],
            loadedConfig.IpcTimeoutMillisecondsByCommand[UcliContractConstants.Config.IpcTimeoutCommandPlan]);
        Assert.Equal(config.OperationAllowlist, loadedConfig.OperationAllowlist);

        var configPath = configStore.GetConfigPath(unityProjectPath);
        using var jsonDocument = JsonDocument.Parse(File.ReadAllText(configPath));
        JsonAssert.For(jsonDocument.RootElement)
            .HasInt32("schemaVersion", UcliContractConstants.Config.SchemaVersion)
            .HasString("operationPolicy", UcliContractConstants.Config.OperationPolicyDangerous)
            .HasString("planTokenMode", UcliContractConstants.Config.PlanTokenModeRequired)
            .HasString("readIndexDefaultMode", UcliContractConstants.Config.ReadIndexModeAllowStale)
            .HasInt32("ipcDefaultTimeoutMilliseconds", 4500)
            .HasProperty("ipcTimeoutMillisecondsByCommand", timeoutByCommand => timeoutByCommand
                .IsNull(UcliContractConstants.Config.IpcTimeoutCommandStatus)
                .HasInt32(UcliContractConstants.Config.IpcTimeoutCommandCall, 16000)
                .HasInt32(UcliContractConstants.Config.IpcTimeoutCommandPlan, 8000))
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

    [Fact]
    [Trait("Size", "Small")]
    public async Task Load_ReturnsInvalidArgument_WhenIpcDefaultTimeoutMillisecondsIsInvalid ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-config-store", "invalid-ipc-timeout-load");
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
                readIndexDefaultMode = UcliContractConstants.Config.ReadIndexModeRequireFresh,
                ipcDefaultTimeoutMilliseconds = 0,
                operationAllowlist = new[] { UcliContractConstants.Config.DefaultOperationAllowlistPattern },
            });
        scope.WriteFile(relativeConfigPath, invalidConfigJson);

        var result = await configStore.Load(unityProjectPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Config);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("ipcDefaultTimeoutMilliseconds", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Load_WithMissingIpcTimeoutByCommand_UsesDefaultCommandEntries ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-config-store", "missing-ipc-timeout-by-command");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var configStore = new UcliConfigStore();
        var configPath = configStore.GetConfigPath(unityProjectPath);
        var relativeConfigPath = Path.GetRelativePath(scope.FullPath, configPath);
        var configJson = JsonSerializer.Serialize(
            new
            {
                schemaVersion = UcliContractConstants.Config.SchemaVersion,
                operationPolicy = UcliContractConstants.Config.OperationPolicySafe,
                planTokenMode = UcliContractConstants.Config.PlanTokenModeOptional,
                readIndexDefaultMode = UcliContractConstants.Config.ReadIndexModeRequireFresh,
                ipcDefaultTimeoutMilliseconds = UcliContractConstants.Config.IpcDefaultTimeoutMilliseconds,
                operationAllowlist = new[] { UcliContractConstants.Config.DefaultOperationAllowlistPattern },
            });
        scope.WriteFile(relativeConfigPath, configJson);

        var result = await configStore.Load(unityProjectPath, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var config = Assert.IsType<UcliConfig>(result.Config);
        AssertDefaultIpcTimeouts(config.IpcTimeoutMillisecondsByCommand);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Save_ReturnsInvalidArgument_WhenIpcDefaultTimeoutMillisecondsIsInvalid ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-config-store", "invalid-ipc-timeout-save");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var configStore = new UcliConfigStore();
        var invalidConfig = new UcliConfig(
            SchemaVersion: UcliContractConstants.Config.SchemaVersion,
            OperationPolicy: OperationPolicy.Safe,
            PlanTokenMode: PlanTokenMode.Optional,
            ReadIndexDefaultMode: ReadIndexMode.RequireFresh,
            OperationAllowlist:
            [
                UcliContractConstants.Config.DefaultOperationAllowlistPattern,
            ])
        {
            IpcDefaultTimeoutMilliseconds = 0,
        };

        var saveResult = await configStore.Save(unityProjectPath, invalidConfig, CancellationToken.None);

        Assert.False(saveResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(saveResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("ipcDefaultTimeoutMilliseconds", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Load_WithEmptyIpcTimeoutByCommandObject_Succeeds ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-config-store", "empty-ipc-timeout-by-command");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var configStore = new UcliConfigStore();
        var configPath = configStore.GetConfigPath(unityProjectPath);
        var relativeConfigPath = Path.GetRelativePath(scope.FullPath, configPath);
        var configJson = JsonSerializer.Serialize(
            new
            {
                schemaVersion = UcliContractConstants.Config.SchemaVersion,
                operationPolicy = UcliContractConstants.Config.OperationPolicySafe,
                planTokenMode = UcliContractConstants.Config.PlanTokenModeOptional,
                readIndexDefaultMode = UcliContractConstants.Config.ReadIndexModeRequireFresh,
                ipcDefaultTimeoutMilliseconds = UcliContractConstants.Config.IpcDefaultTimeoutMilliseconds,
                ipcTimeoutMillisecondsByCommand = new Dictionary<string, int?>(),
                operationAllowlist = new[] { UcliContractConstants.Config.DefaultOperationAllowlistPattern },
            });
        scope.WriteFile(relativeConfigPath, configJson);

        var result = await configStore.Load(unityProjectPath, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var config = Assert.IsType<UcliConfig>(result.Config);
        Assert.Empty(config.IpcTimeoutMillisecondsByCommand);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Load_ReturnsInvalidArgument_WhenIpcTimeoutByCommandContainsUnsupportedCommand ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-config-store", "unsupported-ipc-timeout-command");
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
                readIndexDefaultMode = UcliContractConstants.Config.ReadIndexModeRequireFresh,
                ipcDefaultTimeoutMilliseconds = UcliContractConstants.Config.IpcDefaultTimeoutMilliseconds,
                ipcTimeoutMillisecondsByCommand = new Dictionary<string, int?>
                {
                    ["unknown"] = 3200,
                },
                operationAllowlist = new[] { UcliContractConstants.Config.DefaultOperationAllowlistPattern },
            });
        scope.WriteFile(relativeConfigPath, invalidConfigJson);

        var result = await configStore.Load(unityProjectPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Config);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("ipcTimeoutMillisecondsByCommand", error.Message, StringComparison.Ordinal);
        Assert.Contains("unknown", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Save_ReturnsInvalidArgument_WhenIpcTimeoutByCommandContainsInvalidValue ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-config-store", "invalid-ipc-timeout-by-command-save");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var configStore = new UcliConfigStore();
        var invalidConfig = new UcliConfig(
            SchemaVersion: UcliContractConstants.Config.SchemaVersion,
            OperationPolicy: OperationPolicy.Safe,
            PlanTokenMode: PlanTokenMode.Optional,
            ReadIndexDefaultMode: ReadIndexMode.RequireFresh,
            OperationAllowlist:
            [
                UcliContractConstants.Config.DefaultOperationAllowlistPattern,
            ])
        {
            IpcDefaultTimeoutMilliseconds = UcliContractConstants.Config.IpcDefaultTimeoutMilliseconds,
            IpcTimeoutMillisecondsByCommand = new Dictionary<string, int?>(StringComparer.Ordinal)
            {
                [UcliContractConstants.Config.IpcTimeoutCommandStatus] = 0,
            },
        };

        var saveResult = await configStore.Save(unityProjectPath, invalidConfig, CancellationToken.None);

        Assert.False(saveResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(saveResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("ipcTimeoutMillisecondsByCommand", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Save_ReturnsInvalidArgument_WhenIpcTimeoutByCommandContainsUnsupportedCommand ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-config-store", "unsupported-ipc-timeout-command-save");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var configStore = new UcliConfigStore();
        var invalidConfig = new UcliConfig(
            SchemaVersion: UcliContractConstants.Config.SchemaVersion,
            OperationPolicy: OperationPolicy.Safe,
            PlanTokenMode: PlanTokenMode.Optional,
            ReadIndexDefaultMode: ReadIndexMode.RequireFresh,
            OperationAllowlist:
            [
                UcliContractConstants.Config.DefaultOperationAllowlistPattern,
            ])
        {
            IpcDefaultTimeoutMilliseconds = UcliContractConstants.Config.IpcDefaultTimeoutMilliseconds,
            IpcTimeoutMillisecondsByCommand = new Dictionary<string, int?>(StringComparer.Ordinal)
            {
                ["unsupported"] = 3000,
            },
        };

        var saveResult = await configStore.Save(unityProjectPath, invalidConfig, CancellationToken.None);

        Assert.False(saveResult.IsSuccess);
        var error = Assert.IsType<ExecutionError>(saveResult.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("ipcTimeoutMillisecondsByCommand", error.Message, StringComparison.Ordinal);
        Assert.Contains("unsupported", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Load_ReturnsInvalidArgument_WhenConfigContainsUnknownProperty ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-config-store", "unknown-property");
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
                readIndexDefaultMode = UcliContractConstants.Config.ReadIndexModeRequireFresh,
                ipcDefaultTimeoutMilliseconds = UcliContractConstants.Config.IpcDefaultTimeoutMilliseconds,
                unexpectedProperty = "noise",
                operationAllowlist = new[] { UcliContractConstants.Config.DefaultOperationAllowlistPattern },
            });
        scope.WriteFile(relativeConfigPath, invalidConfigJson);

        var result = await configStore.Load(unityProjectPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Config);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("unknown properties", error.Message, StringComparison.Ordinal);
        Assert.Contains("unexpectedProperty", error.Message, StringComparison.Ordinal);
    }

    private static void AssertDefaultIpcTimeouts (IReadOnlyDictionary<string, int?> actual)
    {
        Assert.Equal(10, actual.Count);
        Assert.True(actual.ContainsKey(UcliContractConstants.Config.IpcTimeoutCommandTest));
        Assert.True(actual.ContainsKey(UcliContractConstants.Config.IpcTimeoutCommandStatus));
        Assert.True(actual.ContainsKey(UcliContractConstants.Config.IpcTimeoutCommandValidate));
        Assert.True(actual.ContainsKey(UcliContractConstants.Config.IpcTimeoutCommandPlan));
        Assert.True(actual.ContainsKey(UcliContractConstants.Config.IpcTimeoutCommandCall));
        Assert.True(actual.ContainsKey(UcliContractConstants.Config.IpcTimeoutCommandResolve));
        Assert.True(actual.ContainsKey(UcliContractConstants.Config.IpcTimeoutCommandQuery));
        Assert.True(actual.ContainsKey(UcliContractConstants.Config.IpcTimeoutCommandRefresh));
        Assert.True(actual.ContainsKey(UcliContractConstants.Config.IpcTimeoutCommandOps));
        Assert.True(actual.ContainsKey(UcliContractConstants.Config.IpcTimeoutCommandDaemon));

        foreach (var entry in actual)
        {
            Assert.Null(entry.Value);
        }
    }
}