using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Shared.Configuration;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.UnityIntegration.Project;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Assets.Access;

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