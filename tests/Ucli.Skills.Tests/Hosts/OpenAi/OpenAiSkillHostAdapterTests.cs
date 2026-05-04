using MackySoft.Ucli.Skills.Hosts.OpenAi;
using MackySoft.Ucli.Skills.Sources;

namespace MackySoft.Ucli.Skills.Tests.Hosts.OpenAi;

public sealed class OpenAiSkillHostAdapterTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void BuildArtifacts_UsesDeterministicYaml ()
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
