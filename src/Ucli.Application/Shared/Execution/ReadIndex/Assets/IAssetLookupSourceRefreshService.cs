using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Contracts;
using UnityExecutionModeValue = MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision.UnityExecutionMode;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Assets;

/// <summary> Fetches live asset lookup snapshots and refreshes persisted read-index artifacts. </summary>
internal interface IAssetLookupSourceRefreshService
{
    /// <summary> Reads one live asset lookup snapshot and attempts to persist refreshed lookup artifacts. </summary>
    ValueTask<AssetLookupRefreshResult> Refresh (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UcliCommand command,
        UnityExecutionModeValue mode,
        TimeSpan timeout,
        string fallbackReason,
        bool failFast = false,
        CancellationToken cancellationToken = default);
}
