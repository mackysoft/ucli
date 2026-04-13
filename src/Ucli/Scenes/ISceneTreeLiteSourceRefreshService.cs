using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Scenes;

/// <summary> Refreshes scene-tree-lite data from live Unity and persists it on a best-effort basis. </summary>
internal interface ISceneTreeLiteSourceRefreshService
{
    /// <summary> Reads one scene-tree-lite snapshot from source and persists it when possible. </summary>
    ValueTask<SceneTreeLiteRefreshResult> Refresh (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UcliCommand command,
        TimeSpan timeout,
        ReadIndexMode readIndexMode,
        string scenePath,
        string fallbackReason,
        CancellationToken cancellationToken = default);
}