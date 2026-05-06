using MackySoft.Ucli.Skills.Hosts.Claude;
using MackySoft.Ucli.Skills.Hosts.Contracts;
using MackySoft.Ucli.Skills.Hosts.Copilot;
using MackySoft.Ucli.Skills.Hosts.OpenAi;
using MackySoft.Ucli.Skills.Hosts.Registration;

namespace MackySoft.Ucli.Skills.Hosts.Official;

/// <summary> Creates the official supported SKILL host adapters. </summary>
public static class OfficialSkillHostAdapters
{
    /// <summary> Creates the deterministic official supported host adapter set. </summary>
    /// <returns> The official host adapter set used by generation, validation, and runtime commands. </returns>
    public static SkillHostAdapterSet CreateSet ()
    {
        return new SkillHostAdapterSet(CreateAdapters());
    }

    private static IReadOnlyList<ISkillHostAdapter> CreateAdapters ()
    {
        return
        [
            new ClaudeSkillHostAdapter(),
            new CopilotSkillHostAdapter(),
            new OpenAiSkillHostAdapter(),
        ];
    }
}
