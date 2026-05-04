using MackySoft.Ucli.Skills.Hosts;
using MackySoft.Ucli.Skills.Shared;

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
}
