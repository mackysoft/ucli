using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;
using MackySoft.Ucli.Application.Features.OperationCatalog.Common.Contracts;

namespace MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops.Projection;

/// <summary> Maps ops catalog snapshots into command-facing <c>ops list</c> results. </summary>
internal interface IOpsListResultMapper
{
    /// <summary> Maps one list read output into <c>ops list</c> service output. </summary>
    /// <param name="output"> The list read output. </param>
    /// <param name="operations"> The operation entries selected for output. </param>
    /// <returns> The mapped service result. </returns>
    OpsListServiceResult Map (
        OpsListReadOutput output,
        IReadOnlyList<OpsCatalogListEntry> operations);
}
