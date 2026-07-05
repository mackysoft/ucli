using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Tests.Helpers.Assurance;

namespace MackySoft.Ucli.Tests;

public sealed class ReadyCliOutputContractTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void ReadyGolden_AutoOneshotPayload_SatisfiesSemanticInvariants ()
    {
        using var document = CliOutputGoldenFiles.ReadJsonDocument("ready", "auto-oneshot-success.json");
        var payload = document.RootElement.GetProperty("payload");

        var result = CliAssuranceSemanticInvariantValidatorFactory.CreateReadyValidator().Validate(payload);

        Assert.True(result.IsValid);
        var claim = Assert.Single(payload.GetProperty("claims").EnumerateArray());
        var validity = claim.GetProperty("validity");
        Assert.Equal(ReadyValidityKindValues.ProbeOnly, validity.GetProperty("kind").GetString());
        Assert.False(validity.GetProperty("guaranteesReusableSession").GetBoolean());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void ReadyGolden_ReadIndexPayload_UsesArtifactOnlySession ()
    {
        using var document = CliOutputGoldenFiles.ReadJsonDocument("ready", "read-index-success.json");
        var payload = document.RootElement.GetProperty("payload");

        var result = CliAssuranceSemanticInvariantValidatorFactory.CreateReadyValidator().Validate(payload);

        Assert.True(result.IsValid);
        Assert.Equal("readIndex", payload.GetProperty("target").GetString());
        Assert.Equal(AssuranceExecutionModeCodec.NotApplicable, payload.GetProperty("resolvedMode").GetString());
        Assert.Equal(AssuranceSessionKindValues.ArtifactOnly, payload.GetProperty("sessionKind").GetString());
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("lifecycle").ValueKind);
        Assert.Equal(JsonValueKind.Object, payload.GetProperty("readIndex").ValueKind);
        Assert.Equal(3, payload.GetProperty("readIndex").GetProperty("artifacts").GetArrayLength());
    }

}
