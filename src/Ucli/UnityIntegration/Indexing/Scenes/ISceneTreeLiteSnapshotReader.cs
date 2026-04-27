using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Shared.Configuration;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Scenes;

/// <summary> Reads one persisted-preview scene-tree-lite snapshot through the shared IPC execution path. </summary>
internal interface ISceneTreeLiteSnapshotReader
{
    /// <summary> Reads one persisted-preview scene-tree-lite snapshot for the specified scene path. </summary>
    ValueTask<SceneTreeLiteSnapshotFetchResult> Read (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UcliCommand command,
        UnityExecutionMode mode,
        TimeSpan timeout,
        string scenePath,
        bool failFast = false,
        CancellationToken cancellationToken = default);
}