namespace MackySoft.Ucli.Tests;

using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Contracts.Configuration;

public sealed class UcliConfigStoreTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Load_ReturnsDefaultConfig_WhenConfigFileDoesNotExist ()
    {
        using var project = UcliConfigStoreTestSupport.CreateProject("load-default");

        var result = await project.Store.LoadAsync(project.UnityProjectPath, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        Assert.Equal(ConfigSource.Default, result.Source);
        var config = Assert.IsType<UcliConfig>(result.Config);
        Assert.Equal(UcliContractConstants.Config.SchemaVersion, config.SchemaVersion);
        Assert.Equal(OperationPolicy.Safe, config.OperationPolicy);
        Assert.Equal(PlanTokenMode.Optional, config.PlanTokenMode);
        Assert.Equal(ReadIndexMode.RequireFresh, config.ReadIndexDefaultMode);
        Assert.Equal(UcliContractConstants.Config.IpcDefaultTimeoutMilliseconds, config.IpcDefaultTimeoutMilliseconds);
        UcliConfigStoreTestSupport.AssertDefaultIpcTimeouts(config.IpcTimeoutMillisecondsByCommand);
        Assert.Equal(new[] { UcliContractConstants.Config.DefaultOperationAllowlistPattern }, config.OperationAllowlist);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Save_ThenLoad_RoundTripsConfigValues ()
    {
        using var project = UcliConfigStoreTestSupport.CreateProject("save-round-trip");
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

        var saveResult = await project.Store.SaveAsync(project.UnityProjectPath, config, CancellationToken.None);

        Assert.True(saveResult.IsSuccess);
        Assert.Null(saveResult.Error);

        var loadResult = await project.Store.LoadAsync(project.UnityProjectPath, CancellationToken.None);
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

        using var jsonDocument = JsonDocument.Parse(File.ReadAllText(project.ConfigPath.Value));
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
}
