using System.Text.Json;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Contracts.Tests.Configuration;

public sealed class UcliConfigJsonContractReaderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryReadStrict_WithValidConfig_Succeeds ()
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
        var result = UcliConfigJsonContractReader.TryReadStrict(
            document.RootElement,
            out var parsed,
            out var error);

        Assert.True(result);
        Assert.Equal(UcliConfigJsonReadError.None, error);
        Assert.Equal(1, parsed.SchemaVersion);
        Assert.Equal("safe", parsed.OperationPolicy);
        Assert.Equal("required", parsed.PlanTokenMode);
        Assert.Equal("requireFresh", parsed.ReadIndexDefaultMode);
        Assert.NotNull(parsed.OperationAllowlist);
        Assert.Equal(["^foo\\."], parsed.OperationAllowlist);
        Assert.Equal(4000, parsed.IpcDefaultTimeoutMilliseconds);
        Assert.NotNull(parsed.IpcTimeoutMillisecondsByCommand);
        Assert.True(parsed.IpcTimeoutMillisecondsByCommand!.ContainsKey("status"));
        Assert.Null(parsed.IpcTimeoutMillisecondsByCommand["status"]);
        Assert.Equal(15000, parsed.IpcTimeoutMillisecondsByCommand["call"]);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryReadStrict_WithUnknownProperty_ReturnsUnknownPropertyError ()
    {
        const string json = """
        {
          "schemaVersion": 1,
          "operationPolicy": "safe",
          "planTokenMode": "optional",
          "operationAllowlist": ["^foo\\."],
          "unexpectedProperty": true
        }
        """;

        using var document = JsonDocument.Parse(json);
        var result = UcliConfigJsonContractReader.TryReadStrict(
            document.RootElement,
            out _,
            out var error);

        Assert.False(result);
        Assert.Equal(UcliConfigJsonReadErrorKind.UnknownProperty, error.Kind);
        Assert.Equal("unexpectedProperty", error.PropertyName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryReadStrict_WithInvalidAllowlistType_ReturnsPropertyTypeMismatch ()
    {
        const string json = """
        {
          "schemaVersion": 1,
          "operationPolicy": "safe",
          "planTokenMode": "optional",
          "operationAllowlist": {}
        }
        """;

        using var document = JsonDocument.Parse(json);
        var result = UcliConfigJsonContractReader.TryReadStrict(
            document.RootElement,
            out _,
            out var error);

        Assert.False(result);
        Assert.Equal(UcliConfigJsonReadErrorKind.PropertyTypeMismatch, error.Kind);
        Assert.Equal(UcliConfigJsonPropertyNames.OperationAllowlist, error.PropertyName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryReadStrict_WithInvalidTimeoutMapEntryType_ReturnsObjectPropertyTypeMismatch ()
    {
        const string json = """
        {
          "schemaVersion": 1,
          "operationPolicy": "safe",
          "planTokenMode": "optional",
          "operationAllowlist": ["^foo\\."],
          "ipcTimeoutMillisecondsByCommand": {
            "status": "bad"
          }
        }
        """;

        using var document = JsonDocument.Parse(json);
        var result = UcliConfigJsonContractReader.TryReadStrict(
            document.RootElement,
            out _,
            out var error);

        Assert.False(result);
        Assert.Equal(UcliConfigJsonReadErrorKind.ObjectPropertyTypeMismatch, error.Kind);
        Assert.Equal("ipcTimeoutMillisecondsByCommand.status", error.PropertyName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryReadPlanTokenLoose_WithInvalidPlanTokenType_ReturnsNullPlanTokenMode ()
    {
        const string json = """
        {
          "planTokenMode": 1,
          "operationPolicy": " safe ",
          "operationAllowlist": ["^foo\\.", " ", "^bar\\."]
        }
        """;

        using var document = JsonDocument.Parse(json);
        var result = UcliConfigJsonContractReader.TryReadPlanTokenLoose(
            document.RootElement,
            out var parsed,
            out var error);

        Assert.True(result);
        Assert.Equal(UcliConfigJsonReadError.None, error);
        Assert.Null(parsed.PlanTokenMode);
        Assert.Equal("safe", parsed.OperationPolicy);
        Assert.NotNull(parsed.OperationAllowlist);
        Assert.Equal(["^foo\\.", "^bar\\."], parsed.OperationAllowlist);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryReadPlanTokenLoose_WithInvalidAllowlistElement_ReturnsNullAllowlist ()
    {
        const string json = """
        {
          "planTokenMode": "required",
          "operationPolicy": "safe",
          "operationAllowlist": ["^foo\\.", 1]
        }
        """;

        using var document = JsonDocument.Parse(json);
        var result = UcliConfigJsonContractReader.TryReadPlanTokenLoose(
            document.RootElement,
            out var parsed,
            out var error);

        Assert.True(result);
        Assert.Equal(UcliConfigJsonReadError.None, error);
        Assert.Null(parsed.OperationAllowlist);
    }
}
