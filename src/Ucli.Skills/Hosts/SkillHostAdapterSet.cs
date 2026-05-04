using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Hosts;

/// <summary> Provides the deterministic global host adapter set. </summary>
public sealed class SkillHostAdapterSet
{
    private readonly ISkillHostAdapter[] adapters =
    [
        new ClaudeSkillHostAdapter(),
        new CopilotSkillHostAdapter(),
        new OpenAiSkillHostAdapter(),
    ];

    /// <summary> Gets all host adapters in deterministic order. </summary>
    public IReadOnlyList<ISkillHostAdapter> Adapters => adapters;

    /// <summary> Gets one adapter by host key. </summary>
    /// <param name="host"> The host key. </param>
    /// <returns> The adapter or unsupported-host failure. </returns>
    public SkillOperationResult<ISkillHostAdapter> GetAdapter (string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return SkillOperationResult<ISkillHostAdapter>.FailureResult(
                SkillFailureCodes.HostUnsupported,
                $"Unsupported SKILL host: {host ?? "(null)"}");
        }

        foreach (var adapter in adapters)
        {
            if (string.Equals(adapter.Descriptor.HostKey, host, StringComparison.OrdinalIgnoreCase))
            {
                return SkillOperationResult<ISkillHostAdapter>.Success(adapter);
            }
        }

        return SkillOperationResult<ISkillHostAdapter>.FailureResult(
            SkillFailureCodes.HostUnsupported,
            $"Unsupported SKILL host: {host}");
    }
}
