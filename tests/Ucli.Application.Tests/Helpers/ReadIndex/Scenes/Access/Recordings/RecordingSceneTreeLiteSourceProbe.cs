using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingSceneTreeLiteSourceProbe : ISceneTreeLiteSourceProbe
{
    private readonly List<Invocation> invocations = [];

    public SceneTreeLiteSourceProbeResult Result { get; set; } = SceneTreeLiteSourceProbeResult.Success();

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<SceneTreeLiteSourceProbeResult> EnsureCurrentAssetsSceneExistsAsync (
        ResolvedUnityProjectContext project,
        SceneAssetPath scenePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(project, scenePath, cancellationToken));
        return ValueTask.FromResult(Result);
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext Project,
        SceneAssetPath ScenePath,
        CancellationToken CancellationToken);
}
