using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Hosts;

/// <summary> Provides descriptors for the global supported host set. </summary>
public sealed class SkillHostRegistry
{
    private readonly SkillHostDescriptor[] descriptors;

    /// <summary> Initializes a new instance of the <see cref="SkillHostRegistry" /> class. </summary>
    /// <param name="hostAdapters"> The supported host adapter set. </param>
    public SkillHostRegistry (SkillHostAdapterSet? hostAdapters = null)
    {
        descriptors = (hostAdapters ?? new SkillHostAdapterSet()).Adapters
            .Select(static adapter => adapter.Descriptor)
            .OrderBy(static descriptor => descriptor.HostName, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary> Gets all supported host descriptors in deterministic order. </summary>
    public IReadOnlyList<SkillHostDescriptor> Descriptors => descriptors;

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
        foreach (var descriptor in descriptors)
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
