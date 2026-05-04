using MackySoft.Ucli.Skills.Hosts.Claude;
using MackySoft.Ucli.Skills.Hosts.Copilot;
using MackySoft.Ucli.Skills.Hosts.OpenAi;
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
        var service = new SkillMaterializationService();

        foreach (var package in packages)
        {
            foreach (var host in new[] { ClaudeSkillHostAdapter.HostKey, CopilotSkillHostAdapter.HostKey, OpenAiSkillHostAdapter.HostKey })
            {
                var first = service.Materialize(package, host);
                var second = service.Materialize(package, host);

                Assert.True(first.IsSuccess, first.Failure?.Message);
                Assert.True(second.IsSuccess, second.Failure?.Message);
                Assert.Equal(first.Value!.Files, second.Value!.Files);
                var skillText = first.Value.Files.Single(static file => file.RelativePath == "SKILL.md").Content;
                Assert.StartsWith("---\n", skillText, StringComparison.Ordinal);
                Assert.Contains($"name: \"{package.SkillName}\"", skillText, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Materialize_ReturnsUnsupportedHostFailure_WhenHostIsUnknown ()
    {
        var package = (await SkillTestData.GenerateOfficialPackagesAsync()).First();
        var service = new SkillMaterializationService();

        var result = service.Materialize(package, "generic");

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.HostUnsupported, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Materialize_OpenAiAddsOpenAiMetadataOnly ()
    {
        var package = (await SkillTestData.GenerateOfficialPackagesAsync()).First();
        var service = new SkillMaterializationService();

        var openAi = service.Materialize(package, OpenAiSkillHostAdapter.HostKey);
        var claude = service.Materialize(package, ClaudeSkillHostAdapter.HostKey);

        Assert.True(openAi.IsSuccess, openAi.Failure?.Message);
        Assert.True(claude.IsSuccess, claude.Failure?.Message);
        Assert.Contains(openAi.Value!.Files, static file => file.RelativePath == "agents/openai.yaml");
        Assert.DoesNotContain(claude.Value!.Files, static file => file.RelativePath == "agents/openai.yaml");
    }
}
