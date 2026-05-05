using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Shared.Execution.Process;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Assets;

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
        bool failFast = false,
        CancellationToken cancellationToken = default);
}
