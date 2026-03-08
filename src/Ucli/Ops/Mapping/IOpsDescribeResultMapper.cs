using MackySoft.Ucli.Ops.Access;

namespace MackySoft.Ucli.Ops.Mapping;

/// <summary> Maps ops catalog snapshots into command-facing <c>ops describe</c> results. </summary>
internal interface IOpsDescribeResultMapper
{
    /// <summary> Maps one catalog read output into <c>ops describe</c> service output. </summary>
    /// <param name="output"> The catalog read output. </param>
    /// <param name="operationName"> The requested operation name. </param>
    /// <returns> The mapped service result. </returns>
    OpsDescribeServiceResult Map (
        OpsCatalogReadOutput output,
        string? operationName);
}