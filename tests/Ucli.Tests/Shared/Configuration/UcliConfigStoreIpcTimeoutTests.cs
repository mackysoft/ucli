namespace MackySoft.Ucli.Tests;

using System.Text.Json;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Contracts.Configuration;

public sealed class UcliConfigStoreIpcTimeoutTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Load_ReturnsInvalidArgument_WhenIpcDefaultTimeoutMillisecondsIsInvalid ()
    {
        using var project = UcliConfigStoreTestSupport.CreateProject("invalid-ipc-timeout-load");
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
        project.WriteConfigJson(invalidConfigJson);

        var result = await project.Store.LoadAsync(project.UnityProjectPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Config);
        Assert.Null(result.Error);
        var diagnostic = UcliConfigStoreTestSupport.AssertSingleDiagnostic(result.Diagnostics, "config.semantic.invalidTimeout");
        Assert.Contains("ipcDefaultTimeoutMilliseconds", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Load_WithMissingIpcTimeoutByCommand_UsesDefaultCommandEntries ()
    {
        using var project = UcliConfigStoreTestSupport.CreateProject("missing-ipc-timeout-by-command");
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
        project.WriteConfigJson(configJson);

        var result = await project.Store.LoadAsync(project.UnityProjectPath, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var config = Assert.IsType<UcliConfig>(result.Config);
        UcliConfigStoreTestSupport.AssertDefaultIpcTimeouts(config.IpcTimeoutMillisecondsByCommand);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Save_ReturnsInvalidArgument_WhenIpcDefaultTimeoutMillisecondsIsInvalid ()
    {
        using var project = UcliConfigStoreTestSupport.CreateProject("invalid-ipc-timeout-save");
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

        var saveResult = await project.Store.SaveAsync(project.UnityProjectPath, invalidConfig, CancellationToken.None);

        Assert.False(saveResult.IsSuccess);
        Assert.Null(saveResult.Error);
        var diagnostic = UcliConfigStoreTestSupport.AssertSingleDiagnostic(saveResult.Diagnostics, "config.save.invalidTimeout");
        Assert.Contains("ipcDefaultTimeoutMilliseconds", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Load_WithEmptyIpcTimeoutByCommandObject_Succeeds ()
    {
        using var project = UcliConfigStoreTestSupport.CreateProject("empty-ipc-timeout-by-command");
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
        project.WriteConfigJson(configJson);

        var result = await project.Store.LoadAsync(project.UnityProjectPath, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var config = Assert.IsType<UcliConfig>(result.Config);
        Assert.Empty(config.IpcTimeoutMillisecondsByCommand);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Load_ReturnsInvalidArgument_WhenIpcTimeoutByCommandContainsUnsupportedCommand ()
    {
        using var project = UcliConfigStoreTestSupport.CreateProject("unsupported-ipc-timeout-command");
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
        project.WriteConfigJson(invalidConfigJson);

        var result = await project.Store.LoadAsync(project.UnityProjectPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Config);
        Assert.Null(result.Error);
        var diagnostic = UcliConfigStoreTestSupport.AssertSingleDiagnostic(result.Diagnostics, "config.semantic.unsupportedTimeoutCommand");
        Assert.Contains("ipcTimeoutMillisecondsByCommand", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("unknown", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Save_ReturnsInvalidArgument_WhenIpcTimeoutByCommandContainsInvalidValue ()
    {
        using var project = UcliConfigStoreTestSupport.CreateProject("invalid-ipc-timeout-by-command-save");
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

        var saveResult = await project.Store.SaveAsync(project.UnityProjectPath, invalidConfig, CancellationToken.None);

        Assert.False(saveResult.IsSuccess);
        Assert.Null(saveResult.Error);
        var diagnostic = UcliConfigStoreTestSupport.AssertSingleDiagnostic(saveResult.Diagnostics, "config.save.invalidTimeout");
        Assert.Contains("ipcTimeoutMillisecondsByCommand", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Save_ReturnsInvalidArgument_WhenIpcTimeoutByCommandContainsUnsupportedCommand ()
    {
        using var project = UcliConfigStoreTestSupport.CreateProject("unsupported-ipc-timeout-command-save");
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

        var saveResult = await project.Store.SaveAsync(project.UnityProjectPath, invalidConfig, CancellationToken.None);

        Assert.False(saveResult.IsSuccess);
        Assert.Null(saveResult.Error);
        var diagnostic = UcliConfigStoreTestSupport.AssertSingleDiagnostic(saveResult.Diagnostics, "config.save.unsupportedTimeoutCommand");
        Assert.Contains("ipcTimeoutMillisecondsByCommand", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("unsupported", diagnostic.Message, StringComparison.Ordinal);
    }
}
