using System.Text.Json;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Tests.Configuration;

public sealed class UcliConfigSchemaValidatorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithValidConfig_ReturnsRawDocument ()
    {
        const string json = """
        {
          "schemaVersion": 1,
          "operationPolicy": "safe",
          "planTokenMode": "required",
          "readIndexDefaultMode": "requireFresh",
          "operationAllowlist": ["^foo\\."],
          "ipcDefaultTimeoutMilliseconds": 4000,
          "ipcTimeoutMillisecondsByCommand": {
            "status": null,
            "call": 15000
          }
        }
        """;
        using var document = JsonDocument.Parse(json);
        var validator = new UcliConfigSchemaValidator();

        var result = validator.Validate(document.RootElement, "config.json");

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Diagnostics);
        Assert.True(result.Document.HasValue);
        var rawDocument = result.Document.Value;
        Assert.Equal(1, rawDocument.SchemaVersion);
        Assert.Equal("safe", rawDocument.OperationPolicy);
        Assert.Equal("required", rawDocument.PlanTokenMode);
        Assert.Equal("requireFresh", rawDocument.ReadIndexDefaultMode);
        Assert.NotNull(rawDocument.OperationAllowlist);
        Assert.Equal(["^foo\\."], rawDocument.OperationAllowlist!);
        Assert.Equal(4000, rawDocument.IpcDefaultTimeoutMilliseconds);
        Assert.NotNull(rawDocument.IpcTimeoutMillisecondsByCommand);
        Assert.Null(rawDocument.IpcTimeoutMillisecondsByCommand!["status"]);
        Assert.Equal(15000, rawDocument.IpcTimeoutMillisecondsByCommand["call"]);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithMultipleSchemaErrors_ReturnsAllDiagnostics ()
    {
        const string json = """
        {
          "schemaVersion": "1",
          "operationAllowlist": ["^foo\\.", 1],
          "ipcTimeoutMillisecondsByCommand": {
            "status": "bad"
          },
          "unexpectedProperty": true
        }
        """;
        using var document = JsonDocument.Parse(json);
        var validator = new UcliConfigSchemaValidator();

        var result = validator.Validate(document.RootElement, "config.json");

        Assert.False(result.IsSuccess);
        Assert.Null(result.Document);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.PropertyPath == UcliConfigJsonPropertyNames.SchemaVersion);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.PropertyPath == UcliConfigJsonPropertyNames.OperationPolicy);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.PropertyPath == UcliConfigJsonPropertyNames.PlanTokenMode);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.PropertyPath == "operationAllowlist[1]");
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.PropertyPath == "ipcTimeoutMillisecondsByCommand.status");
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.PropertyPath == "unexpectedProperty");
    }
}
