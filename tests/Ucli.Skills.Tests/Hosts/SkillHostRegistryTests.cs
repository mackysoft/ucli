using MackySoft.Ucli.Skills.Hosts;
using MackySoft.Ucli.Skills.Shared;
using MackySoft.Ucli.Skills.Sources;

namespace MackySoft.Ucli.Skills.Tests.Hosts;

public sealed class SkillHostRegistryTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void HostRegistry_ContainsSupportedHostsOnly ()
    {
        var registry = new SkillHostRegistry();

        Assert.Equal(
            new[] { SkillHostKindValues.Claude, SkillHostKindValues.Copilot, SkillHostKindValues.OpenAi },
            registry.Descriptors.Select(static descriptor => descriptor.HostName).ToArray());
        Assert.Equal(".claude/skills", registry.GetDescriptor(SkillHostKind.Claude).ProjectTargetDirectory);
        Assert.Equal(".github/skills", registry.GetDescriptor(SkillHostKind.Copilot).ProjectTargetDirectory);
        Assert.Equal(".agents/skills", registry.GetDescriptor(SkillHostKind.OpenAi).ProjectTargetDirectory);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Parse_ReturnsUnsupportedHostFailure ()
    {
        var result = SkillHostKindCodec.Parse("generic");

        Assert.False(result.IsSuccess);
        Assert.Equal(SkillFailureCodes.HostUnsupported, result.Failure!.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void AdapterSet_MatchesHostRegistryDescriptors ()
    {
        var registry = new SkillHostRegistry();
        var adapterSet = new SkillHostAdapterSet();

        Assert.Equal(
            registry.Descriptors.Select(static descriptor => (descriptor.Host, descriptor.HostName, descriptor.ProjectTargetDirectory)).ToArray(),
            adapterSet.Adapters.Select(static adapter => (adapter.Descriptor.Host, adapter.Descriptor.HostName, adapter.Descriptor.ProjectTargetDirectory)).ToArray());
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
