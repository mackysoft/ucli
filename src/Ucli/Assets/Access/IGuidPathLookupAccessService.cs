using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Assets.Access;

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