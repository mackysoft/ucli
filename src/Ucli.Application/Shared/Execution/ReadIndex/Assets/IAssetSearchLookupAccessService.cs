using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Contracts.Configuration;
using UnityExecutionModeValue = MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision.UnityExecutionMode;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Assets;

/// <summary> Reads asset-search lookup data from read-index or Unity source according to execution policy. </summary>
internal interface IAssetSearchLookupAccessService
{
    /// <summary> Searches asset lookup entries using one assets.find-style query. </summary>
    ValueTask<AssetSearchLookupReadResult> SearchAsync (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UnityExecutionModeValue mode,
        TimeSpan timeout,
        ReadIndexMode readIndexMode,
        AssetSearchLookupQuery query,
        bool failFast = false,
        CancellationToken cancellationToken = default);
}
