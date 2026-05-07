using MackySoft.Ucli.Application.Shared.Configuration;
using UnityExecutionModeValue = MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision.UnityExecutionMode;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;

/// <summary> Refreshes scene-tree-lite data from persisted-preview Unity snapshots and persists it on a best-effort basis. </summary>
internal interface ISceneTreeLiteSourceRefreshService
{
    /// <summary> Reads one scene-tree-lite snapshot from source and persists it when possible. </summary>
    ValueTask<SceneTreeLiteRefreshResult> Refresh (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UcliCommand command,
        UnityExecutionModeValue mode,
        TimeSpan timeout,
        string scenePath,
        string fallbackReason,
        bool failFast = false,
        CancellationToken cancellationToken = default);
}
