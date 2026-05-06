using MackySoft.Ucli.Skills.Hosts.Claude;
using MackySoft.Ucli.Skills.Hosts.Copilot;
using MackySoft.Ucli.Skills.Hosts.OpenAi;
using MackySoft.Ucli.Skills.Installation.Validation;
using MackySoft.Ucli.Skills.Packaging;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Tests.Materialization;

public sealed class SkillMaterializationServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Materialize_AllOfficialSkills_ForAllSupportedHosts ()
    {
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var service = SkillTestData.CreateMaterializationService();

        foreach (var package in packages)
        {
            foreach (var host in new[] { ClaudeSkillHostAdapter.HostKey, CopilotSkillHostAdapter.HostKey, OpenAiSkillHostAdapter.HostKey })
            {
                var first = service.Materialize(package, host);
                var second = service.Materialize(package, host);

                Assert.True(first.IsSuccess, first.Failure?.Message);
                Assert.True(second.IsSuccess, second.Failure?.Message);
                Assert.Equal(first.Value!.Files, second.Value!.Files);
                var materializedFiles = first.Value.Files;
                var skillText = materializedFiles.Single(static file => file.RelativePath == "SKILL.md").Content;
                Assert.StartsWith("---\n", skillText, StringComparison.Ordinal);
                Assert.Contains($"name: \"{package.SkillName}\"", skillText, StringComparison.Ordinal);
                Assert.True(SkillHostMaterializationInspector.TryExtractFrontmatter(skillText, out var frontmatter));
                Assert.False(string.IsNullOrWhiteSpace(frontmatter));
                Assert.Equal(package.Files.Single(static file => file.RelativePath == "SKILL.md").Content, GetBodyWithoutFrontmatter(skillText, frontmatter));
                AssertCanonicalFilesArePreserved(package, materializedFiles);
                Assert.Equal(GetExpectedPaths(package.Files, host), materializedFiles.Select(static file => file.RelativePath).ToArray());
            }
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Materialize_ReturnsUnsupportedHostFailure_WhenHostIsUnknown ()
    {
        var package = (await SkillTestData.GenerateOfficialPackagesAsync()).First();
        var service = SkillTestData.CreateMaterializationService();

        var result = service.Materialize(package, "generic");

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.HostUnsupported, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Materialize_OpenAiAddsOpenAiMetadataOnly ()
    {
        var package = (await SkillTestData.GenerateOfficialPackagesAsync()).First();
        var service = SkillTestData.CreateMaterializationService();

        var openAi = service.Materialize(package, OpenAiSkillHostAdapter.HostKey);

        Assert.True(openAi.IsSuccess, openAi.Failure?.Message);
        Assert.Contains(openAi.Value!.Files, static file => file.RelativePath == "agents/openai.yaml");

        foreach (var host in new[] { ClaudeSkillHostAdapter.HostKey, CopilotSkillHostAdapter.HostKey })
        {
            var nonOpenAi = service.Materialize(package, host);

            Assert.True(nonOpenAi.IsSuccess, nonOpenAi.Failure?.Message);
            Assert.DoesNotContain(nonOpenAi.Value!.Files, static file => file.RelativePath == "agents/openai.yaml");
        }
    }

    private static string[] GetExpectedPaths (
        IReadOnlyList<SkillPackageFile> canonicalFiles,
        string host)
    {
        var hostArtifactPaths = canonicalFiles
            .Where(static file => string.Equals(file.RelativePath, "agents/openai.yaml", StringComparison.Ordinal))
            .Select(static file => file.RelativePath)
            .ToHashSet(StringComparer.Ordinal);

        return canonicalFiles
            .Where(file => !hostArtifactPaths.Contains(file.RelativePath))
            .Select(static file => file.RelativePath)
            .Concat(string.Equals(host, OpenAiSkillHostAdapter.HostKey, StringComparison.Ordinal) ? ["agents/openai.yaml"] : [])
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static void AssertCanonicalFilesArePreserved (
        CanonicalSkillPackage package,
        IReadOnlyList<SkillPackageFile> materializedFiles)
    {
        var hostArtifactPaths = package.Manifest.HostArtifacts
            .Select(static artifact => artifact.Path)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var canonicalFile in package.Files.Where(file => !string.Equals(file.RelativePath, "SKILL.md", StringComparison.Ordinal)
            && !hostArtifactPaths.Contains(file.RelativePath)))
        {
            var materializedFile = materializedFiles.Single(file => string.Equals(file.RelativePath, canonicalFile.RelativePath, StringComparison.Ordinal));
            Assert.Equal(canonicalFile.Content, materializedFile.Content);
        }
    }

    private static string GetBodyWithoutFrontmatter (
        string skillText,
        string frontmatter)
    {
        var body = skillText[frontmatter.Length..];
        return body.StartsWith('\n') ? body[1..] : body;
    }
}
