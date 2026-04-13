using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Scenes;

/// <summary> Reads one live scene-tree-lite snapshot through the shared IPC execution path. </summary>
internal interface ISceneTreeLiteSnapshotReader
{
    /// <summary> Reads one live scene-tree-lite snapshot for the specified scene path. </summary>
    ValueTask<SceneTreeLiteSnapshotFetchResult> Read (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UcliCommand command,
        TimeSpan timeout,
        string scenePath,
        CancellationToken cancellationToken = default);
}