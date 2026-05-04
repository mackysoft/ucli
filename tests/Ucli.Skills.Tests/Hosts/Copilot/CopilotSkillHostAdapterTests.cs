using MackySoft.Ucli.Skills.Hosts.Copilot;
using MackySoft.Ucli.Skills.Sources;

namespace MackySoft.Ucli.Skills.Tests.Hosts.Copilot;

public sealed class CopilotSkillHostAdapterTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void BuildArtifacts_UsesDeterministicYaml ()
    {
        var adapter = new CopilotSkillHostAdapter();
        var metadata = new SkillSourceMetadata(
            SkillSourceMetadata.CurrentSchemaVersion,
            "ucli-sample",
            "Sample Skill",
            "Use C:\\Unity\r\nNext",
            []);

        var artifacts = adapter.BuildArtifacts(metadata);

        Assert.Equal(
            "---\n"
            + "name: \"ucli-sample\"\n"
            + "description: \"Use C:\\\\Unity\\r\\nNext\"\n"
            + "user-invocable: true\n"
            + "---\n",
            artifacts.Frontmatter);
        Assert.Empty(artifacts.AdditionalFiles);
    }
}
