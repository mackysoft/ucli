using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Assets.Access;

/// <summary> Reads asset-search lookup data from read-index or Unity source according to execution policy. </summary>
internal interface IAssetSearchLookupAccessService
{
    /// <summary> Searches asset lookup entries using one assets.find-style query. </summary>
    ValueTask<AssetSearchLookupReadResult> Search (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UnityExecutionMode mode,
        TimeSpan timeout,
        ReadIndexMode readIndexMode,
        AssetSearchLookupQuery query,
        CancellationToken cancellationToken = default);
}