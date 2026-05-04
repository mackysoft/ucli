namespace MackySoft.Ucli.Skills.Hosts;

/// <summary> Provides the global supported host adapter set. </summary>
public sealed class SkillHostRegistry
{
    private static readonly SkillHostDescriptor[] HostDescriptors =
    [
        new SkillHostDescriptor(SkillHostKind.Claude, SkillHostKindValues.Claude, ".claude/skills"),
        new SkillHostDescriptor(SkillHostKind.Copilot, SkillHostKindValues.Copilot, ".github/skills"),
        new SkillHostDescriptor(SkillHostKind.OpenAi, SkillHostKindValues.OpenAi, ".agents/skills"),
    ];

    /// <summary> Gets all supported host descriptors in deterministic order. </summary>
    public IReadOnlyList<SkillHostDescriptor> Descriptors => HostDescriptors;

    /// <summary> Gets the descriptor for one host. </summary>
    /// <param name="host"> The host value. </param>
    /// <returns> The host descriptor. </returns>
    public SkillHostDescriptor GetDescriptor (SkillHostKind host)
    {
        foreach (var descriptor in HostDescriptors)
        {
            if (descriptor.Host == host)
            {
                return descriptor;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(host), host, "Unsupported SKILL host.");
    }
}
