using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Ready;

namespace MackySoft.Ucli.Tests;

public sealed class ReadyCliOutputContractTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void ReadyGolden_AutoOneshotPayload_UsesProbeOnlyValidity ()
    {
        using var document = CliOutputGoldenFiles.ReadJsonDocument("ready", "auto-oneshot-success.json");
        var payload = document.RootElement.GetProperty("payload");

        var claim = Assert.Single(payload.GetProperty("claims").EnumerateArray());
        var validity = claim.GetProperty("validity");
        Assert.Equal(TextVocabulary.GetText(ReadyValidityKind.ProbeOnly), validity.GetProperty("kind").GetString());
        Assert.False(validity.TryGetProperty("guaranteesReusableSession", out _));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void ReadyGolden_ReadIndexPayload_UsesArtifactOnlySession ()
    {
        using var document = CliOutputGoldenFiles.ReadJsonDocument("ready", "read-index-success.json");
        var payload = document.RootElement.GetProperty("payload");

        Assert.Equal("readIndex", payload.GetProperty("target").GetString());
        Assert.Equal(
            TextVocabulary.GetText(AssuranceResolvedExecutionMode.NotApplicable),
            payload.GetProperty("resolvedMode").GetString());
        Assert.Equal(TextVocabulary.GetText(AssuranceSessionKind.ArtifactOnly), payload.GetProperty("sessionKind").GetString());
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("lifecycle").ValueKind);
        Assert.Equal(JsonValueKind.Object, payload.GetProperty("readIndex").ValueKind);
        Assert.Equal(3, payload.GetProperty("readIndex").GetProperty("artifacts").GetArrayLength());
    }

}
