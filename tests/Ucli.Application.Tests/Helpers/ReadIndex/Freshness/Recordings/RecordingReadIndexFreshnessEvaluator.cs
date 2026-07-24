using MackySoft.Ucli.Contracts.Cryptography;

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
        Sha256Digest persistedSourceInputsHash,
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
        SceneTreeLiteSourcePaths sourcePaths,
        Sha256Digest persistedSourceInputsHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);
        cancellationToken.ThrowIfCancellationRequested();
        sceneTreeLiteObserveInvocations.Add(new SceneTreeLiteObserveInvocation(
            sourcePaths,
            persistedSourceInputsHash,
            cancellationToken));
        return ValueTask.FromResult(Result);
    }

    internal readonly record struct ObserveInvocation (
        ResolvedUnityProjectContext UnityProject,
        IndexFreshnessTarget Target,
        Sha256Digest PersistedSourceInputsHash,
        CancellationToken CancellationToken);

    internal readonly record struct SceneTreeLiteObserveInvocation (
        SceneTreeLiteSourcePaths SourcePaths,
        Sha256Digest PersistedSourceInputsHash,
        CancellationToken CancellationToken);
}
