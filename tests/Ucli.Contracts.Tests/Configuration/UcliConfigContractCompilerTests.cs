using System.Text.Json;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Contracts.Tests.Configuration;

public sealed class UcliConfigContractCompilerTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Compile_WithValidEvalConfig_ProducesSharedSnapshot ()
    {
        const string json = """
        {
          "schemaVersion": 1,
          "operationPolicy": "safe",
          "planTokenMode": "required",
          "operationAllowlist": [],
          "evalEnabled": true,
          "ipcTimeoutMillisecondsByCommand": { "eval": 60000 }
        }
        """;
        using var document = JsonDocument.Parse(json);

        var result = new UcliConfigContractCompiler().Compile(document.RootElement, "config.json");

        Assert.True(result.IsSuccess);
        var snapshot = Assert.IsType<UcliConfigContractSnapshot>(result.Snapshot);
        Assert.True(snapshot.EvalEnabled);
        Assert.Equal(3000, snapshot.IpcDefaultTimeoutMilliseconds);
        Assert.Equal(60000, snapshot.IpcTimeoutMillisecondsByCommand!["eval"]);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Compile_WithUnknownPropertyAndInvalidEvalType_FailsClosed ()
    {
        const string json = """
        {
          "schemaVersion": 1,
          "operationPolicy": "safe",
          "planTokenMode": "optional",
          "operationAllowlist": [],
          "evalEnabled": "true",
          "unknown": true
        }
        """;
        using var document = JsonDocument.Parse(json);

        var result = new UcliConfigContractCompiler().Compile(document.RootElement, "config.json");

        Assert.False(result.IsSuccess);
        Assert.Null(result.Snapshot);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == "config.schema.propertyTypeMismatch" && diagnostic.PropertyPath == "evalEnabled");
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == "config.schema.unknownProperty" && diagnostic.PropertyPath == "unknown");
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(false)]
    [InlineData(true)]
    public void Compile_PreservesWhetherTheCommandTimeoutMapWasSpecified (bool includeEmptyMap)
    {
        var mapProperty = includeEmptyMap
            ? "\"ipcTimeoutMillisecondsByCommand\": {},"
            : string.Empty;
        using var document = JsonDocument.Parse($$"""
        {
          "schemaVersion": 1,
          "operationPolicy": "safe",
          "planTokenMode": "optional",
          {{mapProperty}}
          "operationAllowlist": []
        }
        """);

        var result = new UcliConfigContractCompiler().Compile(document.RootElement, "config.json");

        Assert.True(result.IsSuccess);
        if (includeEmptyMap)
        {
            Assert.Empty(result.Snapshot!.IpcTimeoutMillisecondsByCommand!);
        }
        else
        {
            Assert.Null(result.Snapshot!.IpcTimeoutMillisecondsByCommand);
        }
    }
}
