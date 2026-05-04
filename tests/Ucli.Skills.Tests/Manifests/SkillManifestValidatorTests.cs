using MackySoft.Ucli.Skills.Manifests;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Tests.Manifests;

public sealed class SkillManifestValidatorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Validate_AcceptsSafeSkillName ()
    {
        var validator = SkillTestData.CreateManifestValidator();

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
        var validator = SkillTestData.CreateManifestValidator();

        var result = validator.Validate(CreateManifest(skillName));

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
    }

    [Theory]
    [MemberData(nameof(InvalidManifestCases))]
    [Trait("Size", "Small")]
    public void Validate_RejectsInvalidManifestShape (SkillManifest manifest)
    {
        var validator = SkillTestData.CreateManifestValidator();

        var result = validator.Validate(manifest);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
    }

    public static TheoryData<SkillManifest> InvalidManifestCases ()
    {
        var valid = CreateManifest("sample-skill");
        return new TheoryData<SkillManifest>
        {
            valid with { SchemaVersion = 0 },
            valid with { ContentDigest = "sha256:not-hex" },
            valid with { HostArtifacts = valid.HostArtifacts.Where(static artifact => artifact.Host != "copilot").ToArray() },
            valid with { HostArtifacts = valid.HostArtifacts.Concat([new SkillHostArtifactManifest("generic", null, null, "sha256:" + new string('5', 64))]).ToArray() },
            valid with { HostArtifacts = valid.HostArtifacts.Select(static artifact => artifact.Host == "claude" ? artifact with { MaterializedFrontmatterDigest = "sha256:not-hex" } : artifact).ToArray() },
            valid with { HostArtifacts = valid.HostArtifacts.Select(static artifact => artifact.Host == "claude" ? artifact with { Path = "claude.yaml", Digest = "sha256:" + new string('6', 64) } : artifact).ToArray() },
            valid with { HostArtifacts = valid.HostArtifacts.Select(static artifact => artifact.Host == "openai" ? artifact with { Path = "agents/other.yaml" } : artifact).ToArray() },
            valid with { HostArtifacts = valid.HostArtifacts.Select(static artifact => artifact.Host == "openai" ? artifact with { Digest = null } : artifact).ToArray() },
        };
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
