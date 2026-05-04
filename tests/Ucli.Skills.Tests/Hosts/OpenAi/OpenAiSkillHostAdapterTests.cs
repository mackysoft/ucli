using MackySoft.Ucli.Skills.Hosts.Contracts;
using MackySoft.Ucli.Skills.Hosts.OpenAi;

namespace MackySoft.Ucli.Skills.Tests.Hosts.OpenAi;

public sealed class OpenAiSkillHostAdapterTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void BuildArtifacts_UsesDeterministicYaml ()
    {
        var adapter = new OpenAiSkillHostAdapter();
        var metadata = new SkillHostMetadata(
            "ucli-sample",
            "Sample \"Skill\"\rName",
            "Use C:\\Unity\r\nNext"
        );

        var artifacts = adapter.BuildArtifacts(metadata);

        Assert.Equal(
            "---\n"
            + "name: \"ucli-sample\"\n"
            + "description: \"Use C:\\\\Unity\\r\\nNext\"\n"
            + "---\n",
            artifacts.Frontmatter);
        Assert.Equal(
            "interface:\n"
            + "  display_name: \"Sample \\\"Skill\\\"\\rName\"\n"
            + "  short_description: \"Use C:\\\\Unity\\r\\nNext\"\n"
            + "  default_prompt: \"Use $ucli-sample to follow the Sample \\\"Skill\\\"\\rName workflow.\"\n"
            + "\n"
            + "policy:\n"
            + "  allow_implicit_invocation: true\n",
            artifacts.MetadataContent);
    }
}
