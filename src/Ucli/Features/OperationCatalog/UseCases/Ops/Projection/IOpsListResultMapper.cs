using MackySoft.Ucli.Features.OperationCatalog.Catalog.Access;
using MackySoft.Ucli.Features.OperationCatalog.Common.Contracts;

namespace MackySoft.Ucli.Features.OperationCatalog.UseCases.Ops.Projection;

/// <summary> Maps ops catalog snapshots into command-facing <c>ops list</c> results. </summary>
internal interface IOpsListResultMapper
{
    /// <summary> Maps one catalog read output into <c>ops list</c> service output. </summary>
    /// <param name="output"> The catalog read output. </param>
    /// <returns> The mapped service result. </returns>
    OpsListServiceResult Map (OpsCatalogReadOutput output);
}
