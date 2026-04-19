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

/// <summary> Reads GUID-path lookup data from read-index or Unity source according to execution policy. </summary>
internal interface IGuidPathLookupAccessService
{
    /// <summary> Resolves one asset GUID to its asset path. </summary>
    ValueTask<GuidPathLookupReadResult> TryResolveAssetGuid (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UnityExecutionMode mode,
        TimeSpan timeout,
        ReadIndexMode readIndexMode,
        string assetGuid,
        CancellationToken cancellationToken = default);

    /// <summary> Resolves one asset path to its asset GUID. </summary>
    ValueTask<GuidPathLookupReadResult> TryResolveAssetPath (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UnityExecutionMode mode,
        TimeSpan timeout,
        ReadIndexMode readIndexMode,
        string assetPath,
        CancellationToken cancellationToken = default);
}