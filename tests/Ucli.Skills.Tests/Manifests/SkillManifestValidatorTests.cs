using MackySoft.Ucli.Skills.Manifests;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Tests.Manifests;

public sealed class SkillManifestValidatorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Validate_AcceptsSafeSkillName ()
    {
        var validator = new SkillManifestValidator();

        var result = validator.Validate(CreateManifest("sample-skill"));

        Assert.True(result.IsSuccess, result.Failure?.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("SampleSkill")]
    [InlineData("../escape")]
    [InlineData("sample/skill")]
    [InlineData(".")]
    [InlineData("-sample")]
    [Trait("Size", "Small")]
    public void Validate_RejectsUnsafeSkillName (string skillName)
    {
        var validator = new SkillManifestValidator();

        var result = validator.Validate(CreateManifest(skillName));

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
    }

    private static SkillManifest CreateManifest (string skillName)
    {
        return new SkillManifest(
            SkillManifest.CurrentSchemaVersion,
            skillName,
            "sha256:" + new string('0', 64),
            [
                new SkillHostArtifactManifest("claude", null, null, "sha256:" + new string('1', 64)),
                new SkillHostArtifactManifest("copilot", null, null, "sha256:" + new string('2', 64)),
                new SkillHostArtifactManifest("openai", "agents/openai.yaml", "sha256:" + new string('3', 64), "sha256:" + new string('4', 64)),
            ]);
    }
}
