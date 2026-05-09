using MackySoft.Ucli.Application.Shared.Configuration;

namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;

/// <summary> Refreshes ops-catalog data from source and persists read-index artifacts on a best-effort basis. </summary>
internal interface IOpsCatalogSourceRefreshService
{
    /// <summary> Reads the ops catalog from source and attempts to persist refreshed read-index artifacts. </summary>
    ValueTask<OpsCatalogSourceRefreshResult> RefreshAsync (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UnityExecutionMode mode,
        TimeSpan timeout,
        bool failFast,
        string fallbackReason,
        CancellationToken cancellationToken = default);
}
