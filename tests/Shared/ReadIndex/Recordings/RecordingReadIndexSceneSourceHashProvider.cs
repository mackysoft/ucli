namespace MackySoft.Ucli.TestSupport;

internal sealed class RecordingReadIndexSceneSourceHashProvider : IReadIndexSceneSourceHashProvider
{
    private readonly Queue<string?> results = new();
    private readonly List<Invocation> invocations = [];

    private RecordingReadIndexSceneSourceHashProvider (bool requireConfiguredResult)
    {
        RequireConfiguredResult = requireConfiguredResult;
    }

    public RecordingReadIndexSceneSourceHashProvider ()
    {
    }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public string? SourceHash { get; set; }

    public bool RequireConfiguredResult { get; set; }

    public static RecordingReadIndexSceneSourceHashProvider ForQueuedResults ()
    {
        return new RecordingReadIndexSceneSourceHashProvider(requireConfiguredResult: true);
    }

    public void Enqueue (string? result)
    {
        results.Enqueue(result);
    }

    public ValueTask<string?> TryComputeAsync (
        ResolvedUnityProjectContext unityProject,
        string scenePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
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
        string ScenePath,
        CancellationToken CancellationToken);
}
