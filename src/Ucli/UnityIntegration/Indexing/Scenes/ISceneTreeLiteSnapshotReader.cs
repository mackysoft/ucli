using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Scenes;

/// <summary> Reads one scene-tree-lite snapshot through the shared IPC execution path. </summary>
internal interface ISceneTreeLiteSnapshotReader
{
    /// <summary> Reads one scene-tree-lite snapshot for the specified scene path. </summary>
    ValueTask<SceneTreeLiteSnapshotFetchResult> ReadAsync (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UcliCommand command,
        UnityExecutionMode mode,
        TimeSpan timeout,
        string scenePath,
        bool failFast = false,
        bool loadedSceneOnly = false,
        CancellationToken cancellationToken = default);
}
