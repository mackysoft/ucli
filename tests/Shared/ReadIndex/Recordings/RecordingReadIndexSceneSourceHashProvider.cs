using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;
using MackySoft.Ucli.Contracts.Cryptography;

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
        SceneTreeLiteSourcePaths sourcePaths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);
        cancellationToken.ThrowIfCancellationRequested();

        invocations.Add(new Invocation(sourcePaths, cancellationToken));
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
        SceneTreeLiteSourcePaths SourcePaths,
        CancellationToken CancellationToken);
}
