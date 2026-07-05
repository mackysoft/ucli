namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingReadIndexFreshnessEvaluator : IReadIndexFreshnessEvaluator
{
    private readonly List<ObserveInvocation> observeInvocations = [];
    private readonly List<SceneTreeLiteObserveInvocation> sceneTreeLiteObserveInvocations = [];

    public IndexFreshnessEvaluationResult Result { get; set; }
        = IndexFreshnessEvaluationResult.Success(IndexFreshness.Fresh);

    public IReadOnlyList<ObserveInvocation> ObserveInvocations => observeInvocations;

    public IReadOnlyList<SceneTreeLiteObserveInvocation> SceneTreeLiteObserveInvocations => sceneTreeLiteObserveInvocations;

    public ValueTask<IndexFreshnessEvaluationResult> ObserveAsync (
        ResolvedUnityProjectContext unityProject,
        IndexFreshnessTarget target,
        string? persistedSourceInputsHash,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        observeInvocations.Add(new ObserveInvocation(
            unityProject,
            target,
            persistedSourceInputsHash,
            cancellationToken));
        return ValueTask.FromResult(Result);
    }

    public ValueTask<IndexFreshnessEvaluationResult> ObserveSceneTreeLiteAsync (
        ResolvedUnityProjectContext unityProject,
        string scenePath,
        string? persistedSourceInputsHash,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        sceneTreeLiteObserveInvocations.Add(new SceneTreeLiteObserveInvocation(
            unityProject,
            scenePath,
            persistedSourceInputsHash,
            cancellationToken));
        return ValueTask.FromResult(Result);
    }

    internal readonly record struct ObserveInvocation (
        ResolvedUnityProjectContext UnityProject,
        IndexFreshnessTarget Target,
        string? PersistedSourceInputsHash,
        CancellationToken CancellationToken);

    internal readonly record struct SceneTreeLiteObserveInvocation (
        ResolvedUnityProjectContext UnityProject,
        string ScenePath,
        string? PersistedSourceInputsHash,
        CancellationToken CancellationToken);
}
