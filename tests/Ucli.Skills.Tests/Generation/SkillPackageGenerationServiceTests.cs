using MackySoft.Tests;
using MackySoft.Ucli.Skills.Digests;
using MackySoft.Ucli.Skills.Hosts.Claude;
using MackySoft.Ucli.Skills.Hosts.Copilot;
using MackySoft.Ucli.Skills.Hosts.OpenAi;
using MackySoft.Ucli.Skills.Manifests;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Tests.Generation;

public sealed class SkillPackageGenerationServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task GenerateAllAsync_GeneratesCanonicalPackagesWithValidManifests ()
    {
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var validator = SkillTestData.CreateManifestValidator();

        Assert.Equal(SkillTestData.ExpectedSkillNames, packages.Select(static package => package.Manifest.SkillName).ToArray());
        foreach (var package in packages)
        {
            Assert.Contains(package.Files, static file => string.Equals(file.RelativePath, "SKILL.md", StringComparison.Ordinal));
            Assert.Contains(package.Files, static file => string.Equals(file.RelativePath, "ucli-skill.json", StringComparison.Ordinal));
            Assert.Contains(package.Files, static file => string.Equals(file.RelativePath, "agents/openai.yaml", StringComparison.Ordinal));
            Assert.True(validator.Validate(package.Manifest).IsSuccess);
            Assert.False(string.IsNullOrWhiteSpace(package.Manifest.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(package.Manifest.Description));
            Assert.Equal(
                new[] { ClaudeSkillHostAdapter.HostKey, CopilotSkillHostAdapter.HostKey, OpenAiSkillHostAdapter.HostKey },
                package.Manifest.HostArtifacts.Select(static artifact => artifact.Host).ToArray());
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GenerateAllAsync_ComputesContentDigestFromBodyAndReferencesOnly ()
    {
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var calculator = new SkillDigestCalculator();

        foreach (var package in packages)
        {
            var expectedDigest = calculator.ComputeDigest(package.Files
                .Where(static file => string.Equals(file.RelativePath, "SKILL.md", StringComparison.Ordinal)
                    || file.RelativePath.StartsWith("references/", StringComparison.Ordinal))
                .Select(static file => new SkillDigestInputFile(file.RelativePath, file.Content)));

            Assert.Equal(expectedDigest, package.Manifest.ContentDigest);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GeneratedManifestJson_RoundTrips ()
    {
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var serializer = new SkillManifestJsonSerializer();

        foreach (var package in packages)
        {
            var manifestFile = package.Files.Single(static file => string.Equals(file.RelativePath, "ucli-skill.json", StringComparison.Ordinal));
            var manifest = serializer.Deserialize(manifestFile.Content);

            Assert.Equal(SkillManifest.CurrentSchemaVersion, package.Manifest.SchemaVersion);
            Assert.Equal(SkillManifest.CurrentSchemaVersion, manifest.SchemaVersion);
            Assert.Equal(package.Manifest.SkillName, manifest.SkillName);
            Assert.Equal(package.Manifest.DisplayName, manifest.DisplayName);
            Assert.Equal(package.Manifest.Description, manifest.Description);
            Assert.Equal(package.Manifest.ContentDigest, manifest.ContentDigest);
            Assert.Equal(package.Manifest.HostArtifacts, manifest.HostArtifacts);
            Assert.Equal(manifestFile.Content, serializer.Serialize(manifest));
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GenerateAllAsync_RejectsEmptyDefinitionsRoot ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "empty-definitions");
        var service = SkillTestData.CreatePackageGenerationService();

        var result = await service.GenerateAllAsync(scope.CreateDirectory("SkillDefinitions"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.SourceInvalid, result.Failure!.Code);
    }
}
