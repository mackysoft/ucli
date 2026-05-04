using MackySoft.Ucli.Skills.Hosts.Contracts;
using MackySoft.Ucli.Skills.Hosts.Copilot;

namespace MackySoft.Ucli.Skills.Tests.Hosts.Copilot;

public sealed class CopilotSkillHostAdapterTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void BuildArtifacts_UsesDeterministicYaml ()
    {
        var adapter = new CopilotSkillHostAdapter();
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
            + "user-invocable: true\n"
            + "---\n",
            artifacts.Frontmatter);
        Assert.Null(artifacts.MetadataContent);
    }
}
