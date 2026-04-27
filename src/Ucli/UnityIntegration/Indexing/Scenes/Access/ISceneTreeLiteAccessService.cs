using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Shared.Configuration;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Scenes.Access;

/// <summary> Reads scene-tree-lite data across persisted lookup and source fallback paths. </summary>
internal interface ISceneTreeLiteAccessService
{
    /// <summary> Reads scene-tree-lite data for one scene path. </summary>
    ValueTask<SceneTreeLiteReadResult> Read (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UcliCommand command,
        UnityExecutionMode mode,
        TimeSpan timeout,
        ReadIndexMode readIndexMode,
        string scenePath,
        int? depth,
        bool failFast = false,
        CancellationToken cancellationToken = default);
}