using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;

namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;

/// <summary> Refreshes ops-catalog data from source and persists read-index artifacts on a best-effort basis. </summary>
internal interface IOpsCatalogSourceRefreshService
{
    /// <summary> Reads the ops catalog from source and attempts to persist refreshed read-index artifacts. </summary>
    ValueTask<OpsCatalogSourceRefreshResult> Refresh (
        OpsPreflightContext context,
        string fallbackReason,
        CancellationToken cancellationToken = default);
}
