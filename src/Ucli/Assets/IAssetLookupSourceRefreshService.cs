using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Assets;

/// <summary> Fetches live asset lookup snapshots and refreshes persisted read-index artifacts. </summary>
internal interface IAssetLookupSourceRefreshService
{
    /// <summary> Reads one live asset lookup snapshot and attempts to persist refreshed lookup artifacts. </summary>
    ValueTask<AssetLookupRefreshResult> Refresh (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UcliCommand command,
        UnityExecutionMode mode,
        TimeSpan timeout,
        ReadIndexMode readIndexMode,
        string fallbackReason,
        CancellationToken cancellationToken = default);
}