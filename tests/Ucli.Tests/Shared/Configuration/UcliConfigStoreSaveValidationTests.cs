namespace MackySoft.Ucli.Tests;

using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Contracts.Configuration;

public sealed class UcliConfigStoreSaveValidationTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Save_ReturnsInvalidArgument_WhenOperationAllowlistPatternIsInvalid ()
    {
        using var project = UcliConfigStoreTestSupport.CreateProject("invalid-allowlist-save");
        var invalidConfig = new UcliConfig(
            SchemaVersion: UcliContractConstants.Config.SchemaVersion,
            OperationPolicy: OperationPolicy.Safe,
            PlanTokenMode: PlanTokenMode.Optional,
            ReadIndexDefaultMode: ReadIndexMode.RequireFresh,
            OperationAllowlist:
            [
                "[",
            ]);

        var saveResult = await project.Store.SaveAsync(project.UnityProjectPath, invalidConfig, CancellationToken.None);

        Assert.False(saveResult.IsSuccess);
        Assert.Null(saveResult.Error);
        var diagnostic = UcliConfigStoreTestSupport.AssertSingleDiagnostic(saveResult.Diagnostics, "config.save.invalidRegexPattern");
        Assert.Contains("operationAllowlist", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("regex", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Save_ReturnsDiagnostics_WhenConfigContainsMultipleInvalidValues ()
    {
        using var project = UcliConfigStoreTestSupport.CreateProject("multiple-save-diagnostics");
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

        var saveResult = await project.Store.SaveAsync(project.UnityProjectPath, invalidConfig, CancellationToken.None);

        Assert.False(saveResult.IsSuccess);
        Assert.Null(saveResult.Error);
        UcliConfigStoreTestSupport.AssertDiagnostic(saveResult.Diagnostics, "config.save.unsupportedSchemaVersion", UcliConfigJsonPropertyNames.SchemaVersion);
        UcliConfigStoreTestSupport.AssertDiagnostic(saveResult.Diagnostics, "config.save.unsupportedEnum", UcliConfigJsonPropertyNames.OperationPolicy);
        UcliConfigStoreTestSupport.AssertDiagnostic(saveResult.Diagnostics, "config.save.unsupportedEnum", UcliConfigJsonPropertyNames.PlanTokenMode);
        UcliConfigStoreTestSupport.AssertDiagnostic(saveResult.Diagnostics, "config.save.unsupportedEnum", UcliConfigJsonPropertyNames.ReadIndexDefaultMode);
        UcliConfigStoreTestSupport.AssertDiagnostic(saveResult.Diagnostics, "config.save.emptyAllowlistPattern", "operationAllowlist[0]");
        UcliConfigStoreTestSupport.AssertDiagnostic(saveResult.Diagnostics, "config.save.invalidRegexPattern", "operationAllowlist[1]");
        UcliConfigStoreTestSupport.AssertDiagnostic(saveResult.Diagnostics, "config.save.invalidTimeout", UcliConfigJsonPropertyNames.IpcDefaultTimeoutMilliseconds);
        UcliConfigStoreTestSupport.AssertDiagnostic(saveResult.Diagnostics, "config.save.invalidTimeout", "ipcTimeoutMillisecondsByCommand.status");
        UcliConfigStoreTestSupport.AssertDiagnostic(saveResult.Diagnostics, "config.save.unsupportedTimeoutCommand", "ipcTimeoutMillisecondsByCommand.unsupported");
    }
}
