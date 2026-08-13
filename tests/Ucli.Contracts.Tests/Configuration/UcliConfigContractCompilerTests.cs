using System.Text.Json;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Contracts.Tests.Configuration;

public sealed class UcliConfigContractCompilerTests
{
    [Theory]
    [InlineData("{\"schemaVersion\":1,\"operationPolicy\":\"safe\",\"planTokenMode\":\"optional\",\"operationAllowlist\":[]}", true)]
    [InlineData("{\"schemaVersion\":1,\"operationPolicy\":\"SAFE\",\"planTokenMode\":\"optional\",\"operationAllowlist\":[]}", false)]
    [InlineData("{\"schemaVersion\":1,\"operationPolicy\":\"safe\",\"planTokenMode\":\"optional\",\"operationAllowlist\":[],\"ipcTimeoutMillisecondsByCommand\":null}", false)]
    [InlineData("{\"schemaVersion\":1,\"operationPolicy\":\"safe\",\"planTokenMode\":\"optional\",\"operationAllowlist\":[],\"workCompletion\":{}}", false)]
    [InlineData("{\"schemaVersion\":1,\"operationPolicy\":\"safe\",\"planTokenMode\":\"optional\",\"operationAllowlist\":[],\"unknown\":true}", false)]
    public void Compile_AndPortableValidatorAgreeOnRepresentativeDocuments (string json, bool expected)
    {
        using var document = JsonDocument.Parse(json);
        var shared = new UcliConfigContractCompiler().Compile(document.RootElement, "config.json").IsSuccess;
        var portable = UcliConfigDocumentValidator.TryValidate(document.RootElement, new Dictionary<string, int> { ["call"] = 3000 }, out _);
        Assert.Equal(expected, shared);
        Assert.Equal(portable, shared);
    }
    [Fact]
    public void Compile_WithNullEntryPreservesBuiltinFallbackMarker ()
    {
        using var document = JsonDocument.Parse("{\"schemaVersion\":1,\"operationPolicy\":\"safe\",\"planTokenMode\":\"optional\",\"operationAllowlist\":[],\"ipcTimeoutMillisecondsByCommand\":{\"call\":null}}");
        var result = new UcliConfigContractCompiler().Compile(document.RootElement, "config.json");
        Assert.True(result.IsSuccess);
        Assert.Null(result.Snapshot!.IpcTimeoutMillisecondsByCommand!["call"]);
    }

    [Fact]
    public void Compile_WithDuplicateWorkCompletionPropertyFails ()
    {
        using var document = JsonDocument.Parse("{\"schemaVersion\":1,\"operationPolicy\":\"safe\",\"planTokenMode\":\"optional\",\"operationAllowlist\":[],\"workCompletion\":{\"requiredProgramPresets\":[],\"requiredProgramPresets\":[]}}");
        var result = new UcliConfigContractCompiler().Compile(document.RootElement, "config.json");
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == "config.schema.duplicateProperty");
    }
    [Fact]
    public void Compile_NormalizesProgramPresetKeysOrdinally ()
    {
        using var document = JsonDocument.Parse("{\"schemaVersion\":1,\"operationPolicy\":\"safe\",\"planTokenMode\":\"optional\",\"operationAllowlist\":[],\"programPresets\":{\"z\":{\"description\":\"z\",\"programPath\":\"z.json\"},\"a\":{\"description\":\"a\",\"programPath\":\"a.json\"}}}");
        var result = new UcliConfigContractCompiler().Compile(document.RootElement, "config.json");
        Assert.True(result.IsSuccess);
        Assert.Equal(["a", "z"], result.Snapshot!.ProgramPresets.Keys);
    }
    [Fact]
    public void Compile_WithNullGlobalTimeout_UsesDefault ()
    {
        using var document = JsonDocument.Parse("{\"schemaVersion\":1,\"operationPolicy\":\"safe\",\"planTokenMode\":\"optional\",\"operationAllowlist\":[],\"ipcDefaultTimeoutMilliseconds\":null}");
        var result = new UcliConfigContractCompiler().Compile(document.RootElement, "config.json");
        Assert.True(result.IsSuccess);
        Assert.Equal(3000, result.Snapshot!.IpcDefaultTimeoutMilliseconds);
    }

    [Fact]
    public void Compile_WithNullTimeoutMap_RejectsProperty ()
    {
        using var document = JsonDocument.Parse("{\"schemaVersion\":1,\"operationPolicy\":\"safe\",\"planTokenMode\":\"optional\",\"operationAllowlist\":[],\"ipcTimeoutMillisecondsByCommand\":null}");
        var result = new UcliConfigContractCompiler().Compile(document.RootElement, "config.json");
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.PropertyPath == "ipcTimeoutMillisecondsByCommand");
    }
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
