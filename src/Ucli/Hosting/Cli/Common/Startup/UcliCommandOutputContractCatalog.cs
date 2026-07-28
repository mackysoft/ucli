using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Startup.OutputContracts;

namespace MackySoft.Ucli.Hosting.Cli.Common.Startup;

/// <summary>
/// Owns the effective success and error payload contracts for the public command tree.
/// </summary>
internal static class UcliCommandOutputContractCatalog
{
    private static readonly IReadOnlyList<UcliCommandOutputContract> Contracts =
        CreateContracts();

    private static readonly IReadOnlyDictionary<string, UcliCommandOutputContract> ContractByCommand =
        Contracts.ToDictionary(static contract => contract.Command, StringComparer.Ordinal);

    public static IReadOnlyList<UcliCommandOutputContract> GetAll ()
    {
        return Contracts;
    }

    public static UcliCommandOutputContract Get (string command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        return ContractByCommand.TryGetValue(command, out var contract)
            ? contract
            : throw new KeyNotFoundException($"No CLI output contract is registered for command '{command}'.");
    }

    private static IReadOnlyList<UcliCommandOutputContract> CreateContracts ()
    {
        var contracts = new List<UcliCommandOutputContract>
        {
            UcliCommandOutputContracts.ErrorOnly(UcliCommandNames.Root),
        };

        foreach (var standalone in UcliCommandCatalogDefinition.StandaloneCommands)
        {
            contracts.Add(standalone.OutputContract);
        }

        foreach (var group in UcliCommandCatalogDefinition.CommandGroups)
        {
            contracts.Add(UcliCommandOutputContracts.ErrorOnly(group.CommandName));
            AddLeafContracts(contracts, group.Leaves);
            foreach (var nestedGroup in group.NestedGroups)
            {
                AddLeafContracts(contracts, nestedGroup.Leaves);
            }
        }

        return Array.AsReadOnly(contracts.ToArray());
    }

    private static void AddLeafContracts (
        ICollection<UcliCommandOutputContract> contracts,
        IReadOnlyList<UcliCommandCatalog.CommandLeafEntry> leaves)
    {
        foreach (var leaf in leaves)
        {
            contracts.Add(leaf.OutputContract);
        }
    }
}
