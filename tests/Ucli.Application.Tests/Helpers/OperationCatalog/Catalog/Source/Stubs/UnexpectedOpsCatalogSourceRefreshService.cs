using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class UnexpectedOpsCatalogSourceRefreshService : IOpsCatalogSourceRefreshService
{
    public ValueTask<OpsCatalogSourceRefreshResult> RefreshAsync (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UnityExecutionMode mode,
        TimeSpan timeout,
        bool failFast,
        string fallbackReason,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Ops catalog source refresh should not be requested.");
    }
}
