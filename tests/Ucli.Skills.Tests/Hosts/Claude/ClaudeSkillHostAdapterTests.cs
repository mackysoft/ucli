using MackySoft.Ucli.Skills.Hosts.Claude;
using MackySoft.Ucli.Skills.Hosts.Contracts;

namespace MackySoft.Ucli.Skills.Tests.Hosts.Claude;

public sealed class ClaudeSkillHostAdapterTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void BuildArtifacts_UsesDeterministicYaml ()
    {
        var adapter = new ClaudeSkillHostAdapter();
        var metadata = new SkillHostMetadata(
            "ucli-sample",
            "Sample Skill",
            "Use C:\\Unity\r\nNext"
        );

        var artifacts = adapter.BuildArtifacts(metadata);

        Assert.Equal(
            "---\n"
            + "name: \"ucli-sample\"\n"
            + "description: \"Use C:\\\\Unity\\r\\nNext\"\n"
            + "disable-model-invocation: false\n"
            + "---\n",
            artifacts.Frontmatter);
        Assert.Null(artifacts.MetadataContent);
    }
}
