using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Shared.Configuration;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Scenes;

/// <summary> Refreshes scene-tree-lite data from persisted-preview Unity snapshots and persists it on a best-effort basis. </summary>
internal interface ISceneTreeLiteSourceRefreshService
{
    /// <summary> Reads one scene-tree-lite snapshot from source and persists it when possible. </summary>
    ValueTask<SceneTreeLiteRefreshResult> Refresh (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UcliCommand command,
        UnityExecutionMode mode,
        TimeSpan timeout,
        ReadIndexMode readIndexMode,
        string scenePath,
        string fallbackReason,
        bool failFast = false,
        CancellationToken cancellationToken = default);
}