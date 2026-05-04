using MackySoft.Ucli.Skills.Hosts;
using MackySoft.Ucli.Skills.Shared;
using MackySoft.Ucli.Skills.Sources;

namespace MackySoft.Ucli.Skills.Tests.Hosts;

public sealed class SkillHostAdapterSetTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void AdapterSet_ContainsSupportedHostsOnly ()
    {
        var adapterSet = new SkillHostAdapterSet();

        Assert.Equal(
            new[] { ClaudeSkillHostAdapter.HostKey, CopilotSkillHostAdapter.HostKey, OpenAiSkillHostAdapter.HostKey },
            adapterSet.Adapters.Select(static adapter => adapter.Descriptor.HostKey).ToArray());

        Assert.Equal(".claude/skills", adapterSet.GetAdapter(ClaudeSkillHostAdapter.HostKey).Value!.Descriptor.ProjectTargetDirectory);
        Assert.Equal(".github/skills", adapterSet.GetAdapter(CopilotSkillHostAdapter.HostKey).Value!.Descriptor.ProjectTargetDirectory);
        Assert.Equal(".agents/skills", adapterSet.GetAdapter(OpenAiSkillHostAdapter.HostKey).Value!.Descriptor.ProjectTargetDirectory);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void GetAdapter_ReturnsUnsupportedHostFailure ()
    {
        var adapterSet = new SkillHostAdapterSet();

        var result = adapterSet.GetAdapter("generic");

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.HostUnsupported, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void GetAdapter_CanonicalizesHostKey ()
    {
        var adapterSet = new SkillHostAdapterSet();

        var result = adapterSet.GetAdapter("OpenAI");

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(OpenAiSkillHostAdapter.HostKey, result.Value!.Descriptor.HostKey);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void OpenAiAdapter_BuildArtifacts_UsesDeterministicYaml ()
    {
        var adapter = new OpenAiSkillHostAdapter();
        var metadata = new SkillSourceMetadata(
            SkillSourceMetadata.CurrentSchemaVersion,
            "ucli-sample",
            "Sample \"Skill\"",
            "Use C:\\Unity",
            []);

        var artifacts = adapter.BuildArtifacts(metadata);

        Assert.Equal(
            "---\n"
            + "name: \"ucli-sample\"\n"
            + "description: \"Use C:\\\\Unity\"\n"
            + "---\n",
            artifacts.Frontmatter);
        Assert.Equal(
            "interface:\n"
            + "  display_name: \"Sample \\\"Skill\\\"\"\n"
            + "  short_description: \"Use C:\\\\Unity\"\n"
            + "  default_prompt: \"Use $ucli-sample to follow the Sample \\\"Skill\\\" workflow.\"\n"
            + "\n"
            + "policy:\n"
            + "  allow_implicit_invocation: true\n",
            artifacts.AdditionalFiles.Single().Content);
    }
}
