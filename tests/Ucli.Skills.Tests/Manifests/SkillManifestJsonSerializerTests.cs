using MackySoft.Ucli.Skills.Manifests;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Tests.Manifests;

public sealed class SkillManifestJsonSerializerTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Serialize_UsesLfLineEndings ()
    {
        var serializer = new SkillManifestJsonSerializer();
        var manifest = new SkillManifest(
            SkillManifest.CurrentSchemaVersion,
            "sample-skill",
            "Sample Skill",
            "Use this sample skill for tests.",
            "sha256:" + new string('0', 64),
            [
                new SkillHostArtifactManifest("openai", "agents/openai.yaml", "sha256:" + new string('1', 64), "sha256:" + new string('2', 64)),
                new SkillHostArtifactManifest("claude", null, null, "sha256:" + new string('3', 64)),
                new SkillHostArtifactManifest("copilot", null, null, "sha256:" + new string('4', 64)),
            ]);

        var json = serializer.Serialize(manifest);

        Assert.DoesNotContain("\r\n", json, StringComparison.Ordinal);
        Assert.EndsWith("\n", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("{\"schemaVersion\":1}")]
    [Trait("Size", "Small")]
    public void TryDeserialize_ReturnsManifestInvalid_WhenJsonIsMalformedOrIncomplete (string json)
    {
        var serializer = new SkillManifestJsonSerializer();

        var result = serializer.TryDeserialize(json);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
    }
}
