namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingSceneTreeLiteSourceProbe : ISceneTreeLiteSourceProbe
{
    private readonly List<Invocation> invocations = [];

    public SceneTreeLiteSourceProbeResult Result { get; set; } = SceneTreeLiteSourceProbeResult.Success();

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<SceneTreeLiteSourceProbeResult> EnsureCurrentAssetsSceneExistsAsync (
        SceneTreeLiteSourcePaths sourcePaths,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(sourcePaths, cancellationToken));
        return ValueTask.FromResult(Result);
    }

    internal readonly record struct Invocation (
        SceneTreeLiteSourcePaths SourcePaths,
        CancellationToken CancellationToken);
}
