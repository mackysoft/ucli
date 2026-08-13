using System.Text.Json;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Contracts.Tests.Configuration;

public sealed class UcliConfigDocumentValidatorTests
{
    [Theory]
    [InlineData("{\"schemaVersion\":1,\"operationPolicy\":\"safe\",\"planTokenMode\":\"optional\",\"operationAllowlist\":[\"^ucli\\\\.\"],\"unknown\":true}")]
    [InlineData("{\"schemaVersion\":1,\"schemaVersion\":1,\"operationPolicy\":\"safe\",\"planTokenMode\":\"optional\",\"operationAllowlist\":[\"^ucli\\\\.\"]}")]
    [InlineData("{\"schemaVersion\":1,\"operationPolicy\":\"safe\",\"planTokenMode\":\"optional\",\"operationAllowlist\":[\"(?i)^ucli\\\\.\"]}")]
    [InlineData("{\"schemaVersion\":1,\"operationPolicy\":\"safe\",\"planTokenMode\":\"optional\",\"operationAllowlist\":[\"[\"]}")]
    [InlineData("{\"schemaVersion\":1,\"operationPolicy\":\"safe\",\"planTokenMode\":\"optional\",\"operationAllowlist\":[\"^ucli\\\\.\"],\"ipcTimeoutMillisecondsByCommand\":{\"unknown\":1}}")]
    [InlineData("{\"schemaVersion\":1,\"operationPolicy\":\"safe\",\"planTokenMode\":\"optional\",\"operationAllowlist\":[\"^ucli\\\\.\"],\"programPresets\":{\"bad\":{\"description\":\"x\",\"programPath\":\"../outside.json\"}}}")]
    [InlineData("{\"schemaVersion\":1,\"operationPolicy\":\"safe\",\"planTokenMode\":\"optional\",\"operationAllowlist\":[\"^ucli\\\\.\"],\"workCompletion\":{\"requiredProgramPresets\":[\"valid\",\"valid\"]}}")]
    [Trait("Size", "Small")]
    public void TryValidate_RejectsClosedContractAndSemanticViolations (string json)
    {
        using var document = JsonDocument.Parse(json);

        Assert.False(UcliConfigDocumentValidator.TryValidate(document.RootElement, Defaults, out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidate_ProducesFullEffectiveProjection ()
    {
        using var json = JsonDocument.Parse("""
            {
              "schemaVersion": 1,
              "operationPolicy": "dangerous",
              "planTokenMode": "required",
              "operationAllowlist": ["^ucli\\."],
              "ipcDefaultTimeoutMilliseconds": 42,
              "ipcTimeoutMillisecondsByCommand": { "call": null },
              "evalEnabled": true,
              "programPresets": { "verify-project": { "description": "Verify the project.", "programPath": "programs/verify.json" } },
              "workCompletion": { "requiredProgramPresets": ["verify-project"] }
            }
            """);

        Assert.True(UcliConfigDocumentValidator.TryValidate(json.RootElement, Defaults, out var actual));
        Assert.NotNull(actual);
        Assert.Equal("requireFresh", actual!.ReadIndexDefaultMode);
        Assert.Equal(42, actual.IpcTimeoutMillisecondsByCommand["call"]);
        Assert.Equal(10, actual.IpcTimeoutMillisecondsByCommand["program.run"]);
        Assert.True(actual.EvalEnabled);
    }

    private static readonly IReadOnlyDictionary<string, int> Defaults = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["call"] = 60,
        ["program.run"] = 10,
    };
}
