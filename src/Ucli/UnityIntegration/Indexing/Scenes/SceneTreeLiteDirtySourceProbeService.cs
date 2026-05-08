using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.UnityIntegration.Indexing.Scenes;

/// <summary> Probes daemon-loaded scene state before scene-tree-lite read-index reads. </summary>
internal sealed class SceneTreeLiteDirtySourceProbeService : ISceneTreeLiteDirtySourceProbeService
{
    private const string DirtyLoadedSceneFallbackReason
        = "Dirty loaded scene is open in Unity daemon.";

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
        string scenePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenePath);
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

        var response = fetchResult.Response!;
        return SceneTreeSourceStatePolicy.IsDirtyLiveSource(response.SourceState)
            ? SceneTreeLiteDirtySourceProbeResult.DirtySource(response, DirtyLoadedSceneFallbackReason)
            : SceneTreeLiteDirtySourceProbeResult.NotAvailable("Unity daemon scene is not dirty loaded source.");
    }
}
