using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts;

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
