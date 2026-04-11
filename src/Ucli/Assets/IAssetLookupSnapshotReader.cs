using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Assets;

/// <summary> Reads one live asset lookup snapshot through Unity IPC. </summary>
internal interface IAssetLookupSnapshotReader
{
    /// <summary> Reads one live asset lookup snapshot from Unity. </summary>
    ValueTask<AssetLookupSnapshotFetchResult> Read (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UcliCommand command,
        string? mode,
        string? timeout,
        CancellationToken cancellationToken = default);
}