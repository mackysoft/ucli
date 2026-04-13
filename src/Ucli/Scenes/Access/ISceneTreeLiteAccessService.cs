using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Scenes.Access;

/// <summary> Reads scene-tree-lite data across persisted lookup and source fallback paths. </summary>
internal interface ISceneTreeLiteAccessService
{
    /// <summary> Reads scene-tree-lite data for one scene path. </summary>
    ValueTask<SceneTreeLiteReadResult> Read (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UcliCommand command,
        TimeSpan timeout,
        ReadIndexMode readIndexMode,
        string scenePath,
        int? depth,
        CancellationToken cancellationToken = default);
}