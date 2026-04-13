using MackySoft.Ucli.Ops.Preflight;

namespace MackySoft.Ucli.Ops.Access;

/// <summary> Reads ops catalog data from read-index or source according to execution policy. </summary>
internal interface IOpsCatalogAccessService
{
    /// <summary> Reads one ops catalog according to the resolved preflight context and command input. </summary>
    /// <param name="context"> The preflight-resolved execution context. </param>
    /// <param name="input"> The raw command input values. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to the catalog read result. </returns>
    ValueTask<OpsCatalogReadResult> Read (
        OpsPreflightContext context,
        CancellationToken cancellationToken = default);
}