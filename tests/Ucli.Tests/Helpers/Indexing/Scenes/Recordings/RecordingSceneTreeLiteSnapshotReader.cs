using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.UnityIntegration.Indexing.Scenes;

namespace MackySoft.Ucli.Tests.Helpers.Indexing.Scenes;

internal sealed class RecordingSceneTreeLiteSnapshotReader : ISceneTreeLiteSnapshotReader
{
    private readonly List<Invocation> invocations = [];
    private readonly Queue<SceneTreeLiteSnapshotFetchResult> results = new();

    public IReadOnlyList<Invocation> Invocations => invocations;

    public void Enqueue (SceneTreeLiteSnapshotFetchResult result)
    {
        results.Enqueue(result);
    }

    public ValueTask<SceneTreeLiteSnapshotFetchResult> ReadAsync (
        ResolvedUnityProjectContext project,
        UcliConfig config,
        UcliCommand command,
        UnityExecutionMode mode,
        TimeSpan timeout,
        string scenePath,
        bool failFast = false,
        bool loadedSceneOnly = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        invocations.Add(new Invocation(
            project,
            config,
            command,
            mode,
            timeout,
            scenePath,
            failFast,
            loadedSceneOnly,
            cancellationToken));

        if (!results.TryDequeue(out var result))
        {
            throw new InvalidOperationException("Scene snapshot result is not configured.");
        }

        return ValueTask.FromResult(result);
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext Project,
        UcliConfig Config,
        UcliCommand Command,
        UnityExecutionMode Mode,
        TimeSpan Timeout,
        string ScenePath,
        bool FailFast,
        bool LoadedSceneOnly,
        CancellationToken CancellationToken);
}
