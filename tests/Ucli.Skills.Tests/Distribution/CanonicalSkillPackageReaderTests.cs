using MackySoft.Tests;
using MackySoft.Ucli.Skills.Digests;
using MackySoft.Ucli.Skills.Manifests;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Tests.Distribution;

public sealed class CanonicalSkillPackageReaderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_ReadsGeneratedSkillsMatchingSourceGeneration ()
    {
        var sourcePackages = await SkillTestData.GenerateOfficialPackagesAsync();
        var reader = SkillTestData.CreatePackageReader();

        var generatedPackages = await reader.ReadAllAsync(SkillTestData.GetGeneratedSkillsRoot(), CancellationToken.None);

        Assert.True(generatedPackages.IsSuccess, generatedPackages.Failure?.Message);
        var actualPackages = generatedPackages.Value!;
        Assert.Equal(SkillTestData.ExpectedSkillNames, actualPackages.Select(static package => package.SkillName).ToArray());
        Assert.Equal(
            sourcePackages.SelectMany(static package => package.Files.Select(file => $"{package.SkillName}/{file.RelativePath}={file.Content}")).Order(StringComparer.Ordinal).ToArray(),
            actualPackages.SelectMany(static package => package.Files.Select(file => $"{package.SkillName}/{file.RelativePath}={file.Content}")).Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_RejectsContentDigestDrift ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "generated-content-drift");
        var skillsRoot = CopyGeneratedSkills(scope);
        await File.AppendAllTextAsync(Path.Combine(skillsRoot, SkillTestData.ExpectedSkillNames[0], "SKILL.md"), "\nDrifted body.\n");
        var reader = SkillTestData.CreatePackageReader();

        var result = await reader.ReadAllAsync(skillsRoot, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_RejectsHostArtifactDigestDrift ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "generated-host-artifact-drift");
        var skillsRoot = CopyGeneratedSkills(scope);
        await File.AppendAllTextAsync(Path.Combine(skillsRoot, SkillTestData.ExpectedSkillNames[0], "agents", "openai.yaml"), "\n# drift\n");
        var reader = SkillTestData.CreatePackageReader();

        var result = await reader.ReadAllAsync(skillsRoot, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_RejectsHostArtifactAdapterOutputDrift ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "generated-host-artifact-adapter-drift");
        var skillsRoot = CopyGeneratedSkills(scope);
        var skillDirectory = Path.Combine(skillsRoot, SkillTestData.ExpectedSkillNames[0]);
        var artifactPath = Path.Combine(skillDirectory, "agents", "openai.yaml");
        var driftedArtifact = "interface:\n  display_name: Drifted\n  short_description: Drifted\n  default_prompt: Drifted\n\npolicy:\n  allow_implicit_invocation: false\n";
        await File.WriteAllTextAsync(artifactPath, driftedArtifact);

        var manifestPath = Path.Combine(skillDirectory, "ucli-skill.json");
        var serializer = new SkillManifestJsonSerializer();
        var manifest = serializer.Deserialize(await File.ReadAllTextAsync(manifestPath));
        var driftedDigest = new SkillDigestCalculator().ComputeSingleFileDigest("agents/openai.yaml", driftedArtifact);
        var driftedManifest = manifest with
        {
            HostArtifacts = manifest.HostArtifacts
                .Select(artifact => artifact.Host == "openai"
                    ? artifact with { Digest = driftedDigest }
                    : artifact)
                .ToArray(),
        };
        await File.WriteAllTextAsync(manifestPath, serializer.Serialize(driftedManifest));
        var reader = SkillTestData.CreatePackageReader();

        var result = await reader.ReadAllAsync(skillsRoot, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAllAsync_RejectsFrontmatterDigestDrift ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-skills", "generated-frontmatter-drift");
        var skillsRoot = CopyGeneratedSkills(scope);
        var manifestPath = Path.Combine(skillsRoot, SkillTestData.ExpectedSkillNames[0], "ucli-skill.json");
        var serializer = new SkillManifestJsonSerializer();
        var manifest = serializer.Deserialize(await File.ReadAllTextAsync(manifestPath));
        var driftedManifest = manifest with
        {
            HostArtifacts = manifest.HostArtifacts
                .Select(static artifact => artifact.Host == "claude"
                    ? artifact with { MaterializedFrontmatterDigest = "sha256:" + new string('0', 64) }
                    : artifact)
                .ToArray(),
        };
        await File.WriteAllTextAsync(manifestPath, serializer.Serialize(driftedManifest));
        var reader = SkillTestData.CreatePackageReader();

        var result = await reader.ReadAllAsync(skillsRoot, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.ManifestInvalid, result.Failure!.Code);
    }

    private static string CopyGeneratedSkills (TestDirectoryScope scope)
    {
        var targetRoot = scope.CreateDirectory("skills");
        CopyDirectory(SkillTestData.GetGeneratedSkillsRoot(), targetRoot);
        return targetRoot;
    }

    private static void CopyDirectory (
        string sourceDirectory,
        string targetDirectory)
    {
        foreach (var directoryPath in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(targetDirectory, Path.GetRelativePath(sourceDirectory, directoryPath)));
        }

        foreach (var filePath in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var targetPath = Path.Combine(targetDirectory, Path.GetRelativePath(sourceDirectory, filePath));
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.Copy(filePath, targetPath, overwrite: true);
        }
    }
}
