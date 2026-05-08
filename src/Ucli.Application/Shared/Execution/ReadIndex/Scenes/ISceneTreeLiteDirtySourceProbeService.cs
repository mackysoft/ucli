using MackySoft.Ucli.Application.Shared.Configuration;
using UnityExecutionModeValue = MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision.UnityExecutionMode;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;

/// <summary> Probes for dirty loaded scene-tree-lite source data without using persisted previews. </summary>
internal interface ISceneTreeLiteDirtySourceProbeService
{
    /// <summary> Probes the Unity daemon for a dirty loaded scene snapshot. </summary>
    ValueTask<SceneTreeLiteDirtySourceProbeResult> ProbeAsync (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UcliCommand command,
        UnityExecutionModeValue mode,
        TimeSpan timeout,
        string scenePath,
        CancellationToken cancellationToken = default);
}
