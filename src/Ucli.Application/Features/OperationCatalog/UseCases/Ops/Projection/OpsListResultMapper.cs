using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;
using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Application.Features.OperationCatalog.Common.Contracts;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops.Projection;

/// <summary> Implements mapping from catalog snapshots to command-facing <c>ops list</c> results. </summary>
internal sealed class OpsListResultMapper : IOpsListResultMapper
{
    private readonly OpsReadIndexInfoMapper readIndexInfoMapper;

    /// <summary> Initializes a new instance of the <see cref="OpsListResultMapper" /> class. </summary>
    /// <param name="readIndexInfoMapper"> The read-index info mapper dependency. </param>
    public OpsListResultMapper (OpsReadIndexInfoMapper readIndexInfoMapper)
    {
        this.readIndexInfoMapper = readIndexInfoMapper ?? throw new ArgumentNullException(nameof(readIndexInfoMapper));
    }

    /// <inheritdoc />
    public OpsListServiceResult Map (
        OpsListReadOutput output,
        OpsListFilter filter)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(filter);

        var operations = output.Snapshot.Operations
            .Where(operation => IsMatch(operation, filter))
            .OrderBy(static operation => operation.Name, StringComparer.Ordinal)
            .Select(static operation => new OpsOperationListItem(
                Name: operation.Name!,
                Kind: operation.Kind!,
                Policy: operation.Policy!))
            .ToArray();

        return OpsListServiceResult.Success(
            new OpsListExecutionOutput(
                Operations: operations,
                ReadIndex: readIndexInfoMapper.Map(output.AccessInfo)),
            "uCLI ops list completed.");
    }

    private static bool IsMatch (
        OpsCatalogListEntry operation,
        OpsListFilter filter)
    {
        if (filter.NameRegex != null && !filter.NameRegex.IsMatch(operation.Name))
        {
            return false;
        }

        if (filter.Kind.HasValue)
        {
            if (!UcliOperationKindCodec.TryParse(operation.Kind, out var operationKind)
                || operationKind != filter.Kind.Value)
            {
                return false;
            }
        }

        if (filter.MaxPolicy.HasValue)
        {
            if (!OperationPolicyCodec.TryParse(operation.Policy, out var operationPolicy)
                || operationPolicy > filter.MaxPolicy.Value)
            {
                return false;
            }
        }

        return true;
    }
}
