using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.TestSupport;

internal sealed class RecordingReadIndexSceneSourceHashProvider : IReadIndexSceneSourceHashProvider
{
    private readonly Queue<Sha256Digest?> results = new();
    private readonly List<Invocation> invocations = [];

    private RecordingReadIndexSceneSourceHashProvider (bool requireConfiguredResult)
    {
        RequireConfiguredResult = requireConfiguredResult;
    }

    public RecordingReadIndexSceneSourceHashProvider ()
    {
    }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public Sha256Digest? SourceHash { get; set; }

    public bool RequireConfiguredResult { get; set; }

    public static RecordingReadIndexSceneSourceHashProvider ForQueuedResults ()
    {
        return new RecordingReadIndexSceneSourceHashProvider(requireConfiguredResult: true);
    }

    public void Enqueue (string? result)
    {
        results.Enqueue(result == null ? null : Sha256DigestTestFactory.Compute(result));
    }

    public ValueTask<Sha256Digest?> TryComputeAsync (
        ResolvedUnityProjectContext unityProject,
        SceneAssetPath scenePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(scenePath);
        cancellationToken.ThrowIfCancellationRequested();

        invocations.Add(new Invocation(unityProject, scenePath, cancellationToken));
        if (results.TryDequeue(out var result))
        {
            return ValueTask.FromResult(result);
        }

        if (RequireConfiguredResult)
        {
            throw new InvalidOperationException("Scene source hash result is not configured.");
        }

        return ValueTask.FromResult(SourceHash);
    }

    internal readonly record struct Invocation (
        ResolvedUnityProjectContext UnityProject,
        SceneAssetPath ScenePath,
        CancellationToken CancellationToken);
}
