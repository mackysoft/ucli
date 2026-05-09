using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;
using MackySoft.Ucli.Application.Features.OperationCatalog.Common.Contracts;

namespace MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops.Projection;

/// <summary> Maps ops catalog snapshots into command-facing <c>ops describe</c> results. </summary>
internal interface IOpsDescribeResultMapper
{
    /// <summary> Maps one describe read output into <c>ops describe</c> service output. </summary>
    /// <param name="output"> The describe read output. </param>
    /// <returns> The mapped service result. </returns>
    OpsDescribeServiceResult Map (OpsDescribeReadOutput output);
}
