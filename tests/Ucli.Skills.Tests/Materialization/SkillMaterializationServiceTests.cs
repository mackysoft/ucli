using MackySoft.Ucli.Skills.Hosts.Claude;
using MackySoft.Ucli.Skills.Hosts.Copilot;
using MackySoft.Ucli.Skills.Hosts.OpenAi;
using MackySoft.Ucli.Skills.Installation.Validation;
using MackySoft.Ucli.Skills.Materialization;
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
                AssertCanonicalFilesArePreserved(package.Files, materializedFiles);
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
        return canonicalFiles
            .Select(static file => file.RelativePath)
            .Concat(string.Equals(host, OpenAiSkillHostAdapter.HostKey, StringComparison.Ordinal) ? ["agents/openai.yaml"] : [])
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static void AssertCanonicalFilesArePreserved (
        IReadOnlyList<SkillPackageFile> canonicalFiles,
        IReadOnlyList<SkillPackageFile> materializedFiles)
    {
        foreach (var canonicalFile in canonicalFiles.Where(static file => !string.Equals(file.RelativePath, "SKILL.md", StringComparison.Ordinal)))
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
