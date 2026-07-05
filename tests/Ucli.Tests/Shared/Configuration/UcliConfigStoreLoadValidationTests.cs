namespace MackySoft.Ucli.Tests;

using System.Text.Json;
using MackySoft.Ucli.Contracts.Configuration;

public sealed class UcliConfigStoreLoadValidationTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Load_ReturnsInvalidArgument_WhenConfigJsonIsMalformed ()
    {
        using var project = UcliConfigStoreTestSupport.CreateProject("malformed-json");
        project.WriteConfigJson("{");

        var result = await project.Store.LoadAsync(project.UnityProjectPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Config);
        Assert.Null(result.Error);
        var diagnostic = UcliConfigStoreTestSupport.AssertSingleDiagnostic(result.Diagnostics, "config.json.invalid");
        Assert.Contains("invalid", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Load_ReturnsDiagnostics_WhenConfigContainsMultipleSchemaErrors ()
    {
        using var project = UcliConfigStoreTestSupport.CreateProject("multiple-schema-errors");
        project.WriteConfigJson("""
        {
          "schemaVersion": "1",
          "planTokenMode": 1,
          "operationAllowlist": ["^ucli\\.", 1],
          "unexpectedProperty": true
        }
        """);

        var result = await project.Store.LoadAsync(project.UnityProjectPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Config);
        Assert.Null(result.Error);
        UcliConfigStoreTestSupport.AssertDiagnostic(result.Diagnostics, "config.schema.propertyTypeMismatch", UcliConfigJsonPropertyNames.SchemaVersion);
        UcliConfigStoreTestSupport.AssertDiagnostic(result.Diagnostics, "config.schema.missingProperty", UcliConfigJsonPropertyNames.OperationPolicy);
        UcliConfigStoreTestSupport.AssertDiagnostic(result.Diagnostics, "config.schema.propertyTypeMismatch", UcliConfigJsonPropertyNames.PlanTokenMode);
        UcliConfigStoreTestSupport.AssertDiagnostic(result.Diagnostics, "config.schema.arrayElementTypeMismatch", "operationAllowlist[1]");
        UcliConfigStoreTestSupport.AssertDiagnostic(result.Diagnostics, "config.schema.unknownProperty", "unexpectedProperty");
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Load_ReturnsInvalidArgument_WhenSchemaVersionDoesNotMatch ()
    {
        using var project = UcliConfigStoreTestSupport.CreateProject("schema-version-mismatch");
        var invalidSchemaConfigJson = JsonSerializer.Serialize(
            new
            {
                schemaVersion = 2,
                operationPolicy = UcliContractConstants.Config.OperationPolicySafe,
                planTokenMode = UcliContractConstants.Config.PlanTokenModeOptional,
                readIndexDefaultMode = UcliContractConstants.Config.ReadIndexModeRequireFresh,
                operationAllowlist = new[] { UcliContractConstants.Config.DefaultOperationAllowlistPattern },
            });
        project.WriteConfigJson(invalidSchemaConfigJson);

        var result = await project.Store.LoadAsync(project.UnityProjectPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Config);
        Assert.Null(result.Error);
        var diagnostic = UcliConfigStoreTestSupport.AssertSingleDiagnostic(result.Diagnostics, "config.semantic.unsupportedSchemaVersion");
        Assert.Contains("schemaVersion", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Load_ReturnsInvalidArgument_WhenOperationAllowlistPatternIsInvalid ()
    {
        using var project = UcliConfigStoreTestSupport.CreateProject("invalid-allowlist-load");
        var invalidAllowlistConfigJson = JsonSerializer.Serialize(
            new
            {
                schemaVersion = UcliContractConstants.Config.SchemaVersion,
                operationPolicy = UcliContractConstants.Config.OperationPolicySafe,
                planTokenMode = UcliContractConstants.Config.PlanTokenModeOptional,
                readIndexDefaultMode = UcliContractConstants.Config.ReadIndexModeRequireFresh,
                operationAllowlist = new[] { "[" },
            });
        project.WriteConfigJson(invalidAllowlistConfigJson);

        var result = await project.Store.LoadAsync(project.UnityProjectPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Config);
        Assert.Null(result.Error);
        var diagnostic = UcliConfigStoreTestSupport.AssertSingleDiagnostic(result.Diagnostics, "config.semantic.invalidRegexPattern");
        Assert.Contains("operationAllowlist", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("regex", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Load_ReturnsInvalidArgument_WhenReadIndexDefaultModeIsInvalid ()
    {
        using var project = UcliConfigStoreTestSupport.CreateProject("invalid-read-index-default-mode");
        var invalidConfigJson = JsonSerializer.Serialize(
            new
            {
                schemaVersion = UcliContractConstants.Config.SchemaVersion,
                operationPolicy = UcliContractConstants.Config.OperationPolicySafe,
                planTokenMode = UcliContractConstants.Config.PlanTokenModeOptional,
                readIndexDefaultMode = "unknown",
                operationAllowlist = new[] { UcliContractConstants.Config.DefaultOperationAllowlistPattern },
            });
        project.WriteConfigJson(invalidConfigJson);

        var result = await project.Store.LoadAsync(project.UnityProjectPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Config);
        Assert.Null(result.Error);
        var diagnostic = UcliConfigStoreTestSupport.AssertSingleDiagnostic(result.Diagnostics, "config.semantic.unsupportedLiteral");
        Assert.Contains("readIndexDefaultMode", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Load_ReturnsInvalidArgument_WhenConfigContainsUnknownProperty ()
    {
        using var project = UcliConfigStoreTestSupport.CreateProject("unknown-property");
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
        project.WriteConfigJson(invalidConfigJson);

        var result = await project.Store.LoadAsync(project.UnityProjectPath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Config);
        Assert.Null(result.Error);
        var diagnostic = UcliConfigStoreTestSupport.AssertSingleDiagnostic(result.Diagnostics, "config.schema.unknownProperty");
        Assert.Contains("unknown property", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("unexpectedProperty", diagnostic.Message, StringComparison.Ordinal);
    }
}
