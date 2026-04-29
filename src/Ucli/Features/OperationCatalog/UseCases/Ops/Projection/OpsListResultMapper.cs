using MackySoft.Ucli.Features.OperationCatalog.Catalog.Access;
using MackySoft.Ucli.Features.OperationCatalog.Common.Contracts;

namespace MackySoft.Ucli.Features.OperationCatalog.UseCases.Ops.Projection;

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
    public OpsListServiceResult Map (OpsCatalogReadOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var operations = output.Operations
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
}
