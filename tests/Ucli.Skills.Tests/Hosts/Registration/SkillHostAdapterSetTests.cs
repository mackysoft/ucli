using MackySoft.Ucli.Skills.Hosts.Claude;
using MackySoft.Ucli.Skills.Hosts.Copilot;
using MackySoft.Ucli.Skills.Hosts.OpenAi;
using MackySoft.Ucli.Skills.Hosts.Registration;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Tests.Hosts.Registration;

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
}
