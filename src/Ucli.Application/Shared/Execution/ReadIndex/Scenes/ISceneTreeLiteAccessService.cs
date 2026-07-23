using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using UnityExecutionModeValue = MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision.UnityExecutionMode;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;

/// <summary> Reads scene-tree-lite data across persisted lookup and source fallback paths. </summary>
internal interface ISceneTreeLiteAccessService
{
    /// <summary> Reads scene-tree-lite data for one scene path. </summary>
    ValueTask<SceneTreeLiteReadResult> ReadAsync (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UcliCommand command,
        UnityExecutionModeValue mode,
        TimeSpan timeout,
        ReadIndexMode readIndexMode,
        UnityScenePath scenePath,
        int? depth,
        bool failFast = false,
        CancellationToken cancellationToken = default);

    /// <summary> Reads scene-tree-lite data for one scene path already known to identify an Assets scene. </summary>
    ValueTask<SceneTreeLiteReadResult> ReadAsync (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UcliCommand command,
        UnityExecutionModeValue mode,
        TimeSpan timeout,
        ReadIndexMode readIndexMode,
        SceneAssetPath scenePath,
        int? depth,
        bool failFast = false,
        CancellationToken cancellationToken = default);
}
