using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class UnexpectedAssetLookupSourceRefreshService : IAssetLookupSourceRefreshService
{
    public ValueTask<AssetLookupRefreshResult> RefreshAsync (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UcliCommand command,
        UnityExecutionMode mode,
        TimeSpan timeout,
        string fallbackReason,
        bool failFast = false,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Asset lookup source refresh was not expected.");
    }
}
