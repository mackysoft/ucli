using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Process;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Contracts.Configuration;
using UnityExecutionModeValue = MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision.UnityExecutionMode;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Assets;

/// <summary> Reads GUID-path lookup data from read-index or Unity source according to execution policy. </summary>
internal interface IGuidPathLookupAccessService
{
    /// <summary> Resolves one asset GUID to its asset path. </summary>
    ValueTask<GuidPathLookupReadResult> TryResolveAssetGuid (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UnityExecutionModeValue mode,
        TimeSpan timeout,
        ReadIndexMode readIndexMode,
        string assetGuid,
        CancellationToken cancellationToken = default);

    /// <summary> Resolves one asset path to its asset GUID. </summary>
    ValueTask<GuidPathLookupReadResult> TryResolveAssetPath (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UnityExecutionModeValue mode,
        TimeSpan timeout,
        ReadIndexMode readIndexMode,
        string assetPath,
        CancellationToken cancellationToken = default);
}
