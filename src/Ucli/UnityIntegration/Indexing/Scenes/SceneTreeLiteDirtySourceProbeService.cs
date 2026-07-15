using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Scenes;

/// <summary> Probes daemon-loaded scene state before scene-tree-lite read-index reads. </summary>
internal sealed class SceneTreeLiteDirtySourceProbeService : ISceneTreeLiteDirtySourceProbeService
{
    private readonly ISceneTreeLiteSnapshotReader snapshotReader;

    /// <summary> Initializes a new instance of the <see cref="SceneTreeLiteDirtySourceProbeService" /> class. </summary>
    public SceneTreeLiteDirtySourceProbeService (ISceneTreeLiteSnapshotReader snapshotReader)
    {
        this.snapshotReader = snapshotReader ?? throw new ArgumentNullException(nameof(snapshotReader));
    }

    /// <inheritdoc />
    public async ValueTask<SceneTreeLiteDirtySourceProbeResult> ProbeAsync (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UcliCommand command,
        UnityExecutionMode mode,
        TimeSpan timeout,
        UnityScenePath scenePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(scenePath);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();

        if (mode == UnityExecutionMode.Oneshot)
        {
            return SceneTreeLiteDirtySourceProbeResult.NotAvailable("oneshot execution cannot observe an existing Unity daemon scene.");
        }

        var fetchResult = await snapshotReader.ReadAsync(
                project,
                config,
                command,
                UnityExecutionMode.Daemon,
                timeout,
                scenePath,
                failFast: true,
                loadedSceneOnly: true,
                cancellationToken)
            .ConfigureAwait(false);
        if (!fetchResult.IsSuccess)
        {
            return SceneTreeLiteDirtySourceProbeResult.NotAvailable(fetchResult.Message);
        }

        var snapshot = fetchResult.Snapshot!;
        return SceneTreeSourceStatePolicy.IsDirtyLiveSource(snapshot.SourceState)
            ? SceneTreeLiteDirtySourceProbeResult.DirtySource(snapshot, "Dirty loaded scene is open in Unity daemon.")
            : SceneTreeLiteDirtySourceProbeResult.NotAvailable("Unity daemon scene is not dirty loaded source.");
    }
}
