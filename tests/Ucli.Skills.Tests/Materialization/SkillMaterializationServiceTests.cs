using MackySoft.Ucli.Skills.Hosts;
using MackySoft.Ucli.Skills.Materialization;

namespace MackySoft.Ucli.Skills.Tests.Materialization;

public sealed class SkillMaterializationServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Materialize_AllOfficialSkills_ForAllSupportedHosts ()
    {
        var packages = await SkillTestData.GenerateOfficialPackagesAsync();
        var service = new SkillMaterializationService();

        foreach (var package in packages)
        {
            foreach (var host in new[] { SkillHostKind.Claude, SkillHostKind.Copilot, SkillHostKind.OpenAi })
            {
                var first = service.Materialize(package, host);
                var second = service.Materialize(package, host);

                Assert.True(first.IsSuccess, first.Failure?.Message);
                Assert.True(second.IsSuccess, second.Failure?.Message);
                Assert.Equal(first.Value!.Files, second.Value!.Files);
                Assert.StartsWith("---\n", first.Value.Files.Single(static file => file.RelativePath == "SKILL.md").Content, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Materialize_OpenAiAddsOpenAiMetadataOnly ()
    {
        var package = (await SkillTestData.GenerateOfficialPackagesAsync()).First();
        var service = new SkillMaterializationService();

        var openAi = service.Materialize(package, SkillHostKind.OpenAi);
        var claude = service.Materialize(package, SkillHostKind.Claude);

        Assert.True(openAi.IsSuccess, openAi.Failure?.Message);
        Assert.True(claude.IsSuccess, claude.Failure?.Message);
        Assert.Contains(openAi.Value!.Files, static file => file.RelativePath == "agents/openai.yaml");
        Assert.DoesNotContain(claude.Value!.Files, static file => file.RelativePath == "agents/openai.yaml");
    }
}
