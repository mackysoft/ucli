using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;
using MackySoft.Ucli.Application.Features.OperationCatalog.Common.Contracts;

using MackySoft.Ucli.Contracts.Text;

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
        IReadOnlyList<OpsCatalogListEntry> operations)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(operations);

        var outputOperations = operations
            .OrderBy(static operation => operation.Name, StringComparer.Ordinal)
            .Select(static operation => new OpsOperationListItem(
                Name: operation.Name,
                Kind: TextVocabulary.GetText(operation.Kind),
                Policy: TextVocabulary.GetText(operation.Policy),
                Description: operation.Description))
            .ToArray();

        return OpsListServiceResult.Success(
            new OpsListExecutionOutput(
                Operations: outputOperations,
                ReadIndex: readIndexInfoMapper.Map(output.AccessInfo)),
            "uCLI ops list completed.");
    }
}
