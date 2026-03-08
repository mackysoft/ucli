using MackySoft.Ucli.Ops.Access;

namespace MackySoft.Ucli.Ops.Mapping;

/// <summary> Maps ops catalog snapshots into command-facing <c>ops list</c> results. </summary>
internal interface IOpsListResultMapper
{
    /// <summary> Maps one catalog read output into <c>ops list</c> service output. </summary>
    /// <param name="output"> The catalog read output. </param>
    /// <returns> The mapped service result. </returns>
    OpsListServiceResult Map (OpsCatalogReadOutput output);
}