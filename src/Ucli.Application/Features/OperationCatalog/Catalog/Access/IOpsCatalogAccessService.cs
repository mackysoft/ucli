namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;

/// <summary> Reads ops catalog data from read-index or source according to execution policy. </summary>
internal interface IOpsCatalogAccessService
{
    /// <summary> Reads one lightweight ops catalog according to the resolved preflight context. </summary>
    /// <param name="context"> The preflight-resolved execution context. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to the list read result. </returns>
    ValueTask<OpsListReadResult> ReadListAsync (
        OpsPreflightContext context,
        CancellationToken cancellationToken = default);

    /// <summary> Reads one operation detail according to the resolved preflight context. </summary>
    /// <param name="context"> The preflight-resolved execution context. </param>
    /// <param name="operationName"> The requested operation name. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to the describe read result. </returns>
    ValueTask<OpsDescribeReadResult> ReadDescribeAsync (
        OpsPreflightContext context,
        string? operationName,
        CancellationToken cancellationToken = default);
}
