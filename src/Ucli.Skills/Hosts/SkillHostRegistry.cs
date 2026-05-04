using MackySoft.Ucli.Skills.Shared;

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
        var result = TryGetDescriptor(host);
        if (result.IsSuccess)
        {
            return result.Value!;
        }

        throw new ArgumentOutOfRangeException(nameof(host), host, result.Failure!.Message);
    }

    /// <summary> Gets the descriptor for one host as an operation result. </summary>
    /// <param name="host"> The host value. </param>
    /// <returns> The host descriptor or unsupported-host failure. </returns>
    public SkillOperationResult<SkillHostDescriptor> TryGetDescriptor (SkillHostKind host)
    {
        foreach (var descriptor in HostDescriptors)
        {
            if (descriptor.Host == host)
            {
                return SkillOperationResult<SkillHostDescriptor>.Success(descriptor);
            }
        }

        return SkillOperationResult<SkillHostDescriptor>.FailureResult(
            SkillFailureCodes.HostUnsupported,
            $"Unsupported SKILL host: {host}");
    }
}
