using MackySoft.Ucli.Skills.Hosts.Contracts;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Hosts.Registration;

/// <summary> Provides the deterministic global host adapter set. </summary>
public sealed class SkillHostAdapterSet
{
    private readonly ISkillHostAdapter[] adapters;

    /// <summary> Initializes a new instance of the <see cref="SkillHostAdapterSet" /> class. </summary>
    /// <param name="adapters"> The supported host adapters. </param>
    public SkillHostAdapterSet (IEnumerable<ISkillHostAdapter> adapters)
    {
        ArgumentNullException.ThrowIfNull(adapters);

        var adapterArray = adapters
            .Select(static adapter => adapter ?? throw new ArgumentException("Host adapter collection must not contain null.", nameof(adapters)))
            .ToArray();

        if (adapterArray.Length == 0)
        {
            throw new ArgumentException("At least one host adapter must be provided.", nameof(adapters));
        }

        foreach (var adapter in adapterArray)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(adapter.Descriptor.HostKey, nameof(adapters));
            ArgumentException.ThrowIfNullOrWhiteSpace(adapter.Descriptor.ProjectTargetDirectory, nameof(adapters));
            ArgumentException.ThrowIfNullOrWhiteSpace(adapter.Descriptor.UserTargetDirectory, nameof(adapters));
            ArgumentNullException.ThrowIfNull(adapter.Descriptor.UserTargetRootPolicy, nameof(adapters));
            ArgumentException.ThrowIfNullOrWhiteSpace(adapter.Descriptor.UserTargetRootPolicy.HomeRelativeDirectory, nameof(adapters));
            if (!string.IsNullOrWhiteSpace(adapter.Descriptor.UserTargetRootPolicy.EnvironmentVariableName))
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(adapter.Descriptor.UserTargetRootPolicy.EnvironmentVariableChildDirectory, nameof(adapters));
            }

            ArgumentException.ThrowIfNullOrWhiteSpace(adapter.Descriptor.ReloadGuidance, nameof(adapters));
        }

        var duplicateHost = adapterArray
            .GroupBy(static adapter => adapter.Descriptor.HostKey, StringComparer.OrdinalIgnoreCase)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .FirstOrDefault();

        if (duplicateHost is not null)
        {
            throw new ArgumentException($"Host adapter key must be unique: {duplicateHost}", nameof(adapters));
        }

        this.adapters = adapterArray
            .OrderBy(static adapter => adapter.Descriptor.HostKey, StringComparer.Ordinal)
            .ToArray();
    }

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
