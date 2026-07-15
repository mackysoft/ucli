using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Indexing.Scenes;

namespace MackySoft.Ucli.Tests.Helpers.Indexing.Scenes;

internal sealed class UnexpectedSceneTreeLiteSnapshotReader : ISceneTreeLiteSnapshotReader
{
    private readonly string reason;

    public UnexpectedSceneTreeLiteSnapshotReader (string reason)
    {
        this.reason = reason;
    }

    public ValueTask<SceneTreeLiteSnapshotFetchResult> ReadAsync (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UcliCommand command,
        UnityExecutionMode mode,
        TimeSpan timeout,
        UnityScenePath scenePath,
        bool failFast = false,
        bool loadedSceneOnly = false,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(reason);
    }
}
