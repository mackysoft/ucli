namespace MackySoft.Ucli.Tests;

using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Contracts.Configuration;

public sealed class UcliConfigStoreTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Load_ReturnsDefaultConfig_WhenConfigFileDoesNotExist ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-config-store", "load-default");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var configStore = new UcliConfigStore();

        var result = await configStore.LoadAsync(unityProjectPath, CancellationToken.None);

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

        var saveResult = await configStore.SaveAsync(unityProjectPath, config, CancellationToken.None);

        Assert.True(saveResult.IsSuccess);
        Assert.Null(saveResult.Error);

        var loadResult = await configStore.LoadAsync(unityProjectPath, CancellationToken.None);
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

        var result = await configStore.LoadAsync(unityProjectPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Config);
        Assert.Null(result.Error);
        var diagnostic = AssertSingleDiagnostic(result.Diagnostics, "config.json.invalid");
        Assert.Contains("invalid", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Load_ReturnsDiagnostics_WhenConfigContainsMultipleSchemaErrors ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-config-store", "multiple-schema-errors");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var configStore = new UcliConfigStore();
        var configPath = configStore.GetConfigPath(unityProjectPath);
        var relativeConfigPath = Path.GetRelativePath(scope.FullPath, configPath);
        scope.WriteFile(relativeConfigPath, """
        {
          "schemaVersion": "1",
          "planTokenMode": 1,
          "operationAllowlist": ["^ucli\\.", 1],
          "unexpectedProperty": true
        }
        """);

        var result = await configStore.LoadAsync(unityProjectPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Config);
        Assert.Null(result.Error);
        AssertDiagnostic(result.Diagnostics, "config.schema.propertyTypeMismatch", UcliConfigJsonPropertyNames.SchemaVersion);
        AssertDiagnostic(result.Diagnostics, "config.schema.missingProperty", UcliConfigJsonPropertyNames.OperationPolicy);
        AssertDiagnostic(result.Diagnostics, "config.schema.propertyTypeMismatch", UcliConfigJsonPropertyNames.PlanTokenMode);
        AssertDiagnostic(result.Diagnostics, "config.schema.arrayElementTypeMismatch", "operationAllowlist[1]");
        AssertDiagnostic(result.Diagnostics, "config.schema.unknownProperty", "unexpectedProperty");
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

        var result = await configStore.LoadAsync(unityProjectPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Config);
        Assert.Null(result.Error);
        var diagnostic = AssertSingleDiagnostic(result.Diagnostics, "config.semantic.unsupportedSchemaVersion");
        Assert.Contains("schemaVersion", diagnostic.Message, StringComparison.Ordinal);
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

        var result = await configStore.LoadAsync(unityProjectPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Config);
        Assert.Null(result.Error);
        var diagnostic = AssertSingleDiagnostic(result.Diagnostics, "config.semantic.invalidRegexPattern");
        Assert.Contains("operationAllowlist", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("regex", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
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

        var saveResult = await configStore.SaveAsync(unityProjectPath, invalidConfig, CancellationToken.None);

        Assert.False(saveResult.IsSuccess);
        Assert.Null(saveResult.Error);
        var diagnostic = AssertSingleDiagnostic(saveResult.Diagnostics, "config.save.invalidRegexPattern");
        Assert.Contains("operationAllowlist", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("regex", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
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

        var result = await configStore.LoadAsync(unityProjectPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Config);
        Assert.Null(result.Error);
        var diagnostic = AssertSingleDiagnostic(result.Diagnostics, "config.semantic.unsupportedLiteral");
        Assert.Contains("readIndexDefaultMode", diagnostic.Message, StringComparison.Ordinal);
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

        var result = await configStore.LoadAsync(unityProjectPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Config);
        Assert.Null(result.Error);
        var diagnostic = AssertSingleDiagnostic(result.Diagnostics, "config.semantic.invalidTimeout");
        Assert.Contains("ipcDefaultTimeoutMilliseconds", diagnostic.Message, StringComparison.Ordinal);
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

        var result = await configStore.LoadAsync(unityProjectPath, CancellationToken.None);

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

        var saveResult = await configStore.SaveAsync(unityProjectPath, invalidConfig, CancellationToken.None);

        Assert.False(saveResult.IsSuccess);
        Assert.Null(saveResult.Error);
        var diagnostic = AssertSingleDiagnostic(saveResult.Diagnostics, "config.save.invalidTimeout");
        Assert.Contains("ipcDefaultTimeoutMilliseconds", diagnostic.Message, StringComparison.Ordinal);
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

        var result = await configStore.LoadAsync(unityProjectPath, CancellationToken.None);

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

        var result = await configStore.LoadAsync(unityProjectPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Config);
        Assert.Null(result.Error);
        var diagnostic = AssertSingleDiagnostic(result.Diagnostics, "config.semantic.unsupportedTimeoutCommand");
        Assert.Contains("ipcTimeoutMillisecondsByCommand", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("unknown", diagnostic.Message, StringComparison.Ordinal);
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

        var saveResult = await configStore.SaveAsync(unityProjectPath, invalidConfig, CancellationToken.None);

        Assert.False(saveResult.IsSuccess);
        Assert.Null(saveResult.Error);
        var diagnostic = AssertSingleDiagnostic(saveResult.Diagnostics, "config.save.invalidTimeout");
        Assert.Contains("ipcTimeoutMillisecondsByCommand", diagnostic.Message, StringComparison.Ordinal);
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

        var saveResult = await configStore.SaveAsync(unityProjectPath, invalidConfig, CancellationToken.None);

        Assert.False(saveResult.IsSuccess);
        Assert.Null(saveResult.Error);
        var diagnostic = AssertSingleDiagnostic(saveResult.Diagnostics, "config.save.unsupportedTimeoutCommand");
        Assert.Contains("ipcTimeoutMillisecondsByCommand", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("unsupported", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Save_ReturnsDiagnostics_WhenConfigContainsMultipleInvalidValues ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-config-store", "multiple-save-diagnostics");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var configStore = new UcliConfigStore();
        var invalidConfig = new UcliConfig(
            SchemaVersion: 2,
            OperationPolicy: (OperationPolicy)999,
            PlanTokenMode: (PlanTokenMode)999,
            ReadIndexDefaultMode: (ReadIndexMode)999,
            OperationAllowlist:
            [
                " ",
                "[",
            ])
        {
            IpcDefaultTimeoutMilliseconds = 0,
            IpcTimeoutMillisecondsByCommand = new Dictionary<string, int?>(StringComparer.Ordinal)
            {
                [UcliContractConstants.Config.IpcTimeoutCommandStatus] = 0,
                ["unsupported"] = 3000,
            },
        };

        var saveResult = await configStore.SaveAsync(unityProjectPath, invalidConfig, CancellationToken.None);

        Assert.False(saveResult.IsSuccess);
        Assert.Null(saveResult.Error);
        AssertDiagnostic(saveResult.Diagnostics, "config.save.unsupportedSchemaVersion", UcliConfigJsonPropertyNames.SchemaVersion);
        AssertDiagnostic(saveResult.Diagnostics, "config.save.unsupportedEnum", UcliConfigJsonPropertyNames.OperationPolicy);
        AssertDiagnostic(saveResult.Diagnostics, "config.save.unsupportedEnum", UcliConfigJsonPropertyNames.PlanTokenMode);
        AssertDiagnostic(saveResult.Diagnostics, "config.save.unsupportedEnum", UcliConfigJsonPropertyNames.ReadIndexDefaultMode);
        AssertDiagnostic(saveResult.Diagnostics, "config.save.emptyAllowlistPattern", "operationAllowlist[0]");
        AssertDiagnostic(saveResult.Diagnostics, "config.save.invalidRegexPattern", "operationAllowlist[1]");
        AssertDiagnostic(saveResult.Diagnostics, "config.save.invalidTimeout", UcliConfigJsonPropertyNames.IpcDefaultTimeoutMilliseconds);
        AssertDiagnostic(saveResult.Diagnostics, "config.save.invalidTimeout", "ipcTimeoutMillisecondsByCommand.status");
        AssertDiagnostic(saveResult.Diagnostics, "config.save.unsupportedTimeoutCommand", "ipcTimeoutMillisecondsByCommand.unsupported");
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

        var result = await configStore.LoadAsync(unityProjectPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Config);
        Assert.Null(result.Error);
        var diagnostic = AssertSingleDiagnostic(result.Diagnostics, "config.schema.unknownProperty");
        Assert.Contains("unknown property", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("unexpectedProperty", diagnostic.Message, StringComparison.Ordinal);
    }

    private static UcliConfigDiagnostic AssertSingleDiagnostic (
        IReadOnlyList<UcliConfigDiagnostic> diagnostics,
        string expectedCode)
    {
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(expectedCode, diagnostic.Code);
        return diagnostic;
    }

    private static void AssertDiagnostic (
        IReadOnlyList<UcliConfigDiagnostic> diagnostics,
        string expectedCode,
        string expectedPropertyPath)
    {
        Assert.Contains(
            diagnostics,
            diagnostic => diagnostic.Code == expectedCode
                && diagnostic.PropertyPath == expectedPropertyPath);
    }

    private static void AssertDefaultIpcTimeouts (IReadOnlyDictionary<string, int?> actual)
    {
        Assert.Equal(19, actual.Count);
        Assert.True(actual.ContainsKey(UcliContractConstants.Config.IpcTimeoutCommandTest));
        Assert.True(actual.ContainsKey(UcliContractConstants.Config.IpcTimeoutCommandReady));
        Assert.True(actual.ContainsKey(UcliContractConstants.Config.IpcTimeoutCommandVerify));
        Assert.True(actual.ContainsKey(UcliContractConstants.Config.IpcTimeoutCommandStatus));
        Assert.True(actual.ContainsKey(UcliContractConstants.Config.IpcTimeoutCommandValidate));
        Assert.True(actual.ContainsKey(UcliContractConstants.Config.IpcTimeoutCommandPlan));
        Assert.True(actual.ContainsKey(UcliContractConstants.Config.IpcTimeoutCommandCall));
        Assert.True(actual.ContainsKey(UcliContractConstants.Config.IpcTimeoutCommandResolve));
        Assert.True(actual.ContainsKey(UcliContractConstants.Config.IpcTimeoutCommandQuery));
        Assert.True(actual.ContainsKey(UcliContractConstants.Config.IpcTimeoutCommandRefresh));
        Assert.True(actual.ContainsKey(UcliContractConstants.Config.IpcTimeoutCommandOps));
        Assert.True(actual.ContainsKey(UcliContractConstants.Config.IpcTimeoutCommandDaemonStart));
        Assert.True(actual.ContainsKey(UcliContractConstants.Config.IpcTimeoutCommandDaemonStop));
        Assert.True(actual.ContainsKey(UcliContractConstants.Config.IpcTimeoutCommandDaemonCleanup));
        Assert.True(actual.ContainsKey(UcliContractConstants.Config.IpcTimeoutCommandDaemonStatus));
        Assert.True(actual.ContainsKey(UcliContractConstants.Config.IpcTimeoutCommandDaemonList));
        Assert.True(actual.ContainsKey(UcliContractConstants.Config.IpcTimeoutCommandLogsDaemonRead));
        Assert.True(actual.ContainsKey(UcliContractConstants.Config.IpcTimeoutCommandLogsUnityRead));
        Assert.True(actual.ContainsKey(UcliContractConstants.Config.IpcTimeoutCommandLogsUnityClear));
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultTestMilliseconds, actual[UcliContractConstants.Config.IpcTimeoutCommandTest]);
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultReadyMilliseconds, actual[UcliContractConstants.Config.IpcTimeoutCommandReady]);
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultVerifyMilliseconds, actual[UcliContractConstants.Config.IpcTimeoutCommandVerify]);
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultStatusMilliseconds, actual[UcliContractConstants.Config.IpcTimeoutCommandStatus]);
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultValidateMilliseconds, actual[UcliContractConstants.Config.IpcTimeoutCommandValidate]);
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultPlanMilliseconds, actual[UcliContractConstants.Config.IpcTimeoutCommandPlan]);
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultCallMilliseconds, actual[UcliContractConstants.Config.IpcTimeoutCommandCall]);
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultResolveMilliseconds, actual[UcliContractConstants.Config.IpcTimeoutCommandResolve]);
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultQueryMilliseconds, actual[UcliContractConstants.Config.IpcTimeoutCommandQuery]);
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultRefreshMilliseconds, actual[UcliContractConstants.Config.IpcTimeoutCommandRefresh]);
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultOpsMilliseconds, actual[UcliContractConstants.Config.IpcTimeoutCommandOps]);
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultDaemonStartMilliseconds, actual[UcliContractConstants.Config.IpcTimeoutCommandDaemonStart]);
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultDaemonStopMilliseconds, actual[UcliContractConstants.Config.IpcTimeoutCommandDaemonStop]);
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultDaemonCleanupMilliseconds, actual[UcliContractConstants.Config.IpcTimeoutCommandDaemonCleanup]);
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultDaemonStatusMilliseconds, actual[UcliContractConstants.Config.IpcTimeoutCommandDaemonStatus]);
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultDaemonListMilliseconds, actual[UcliContractConstants.Config.IpcTimeoutCommandDaemonList]);
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultLogsDaemonMilliseconds, actual[UcliContractConstants.Config.IpcTimeoutCommandLogsDaemonRead]);
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultLogsUnityMilliseconds, actual[UcliContractConstants.Config.IpcTimeoutCommandLogsUnityRead]);
        Assert.Equal(UcliContractConstants.Config.IpcTimeoutDefaultLogsUnityClearMilliseconds, actual[UcliContractConstants.Config.IpcTimeoutCommandLogsUnityClear]);
    }
}
