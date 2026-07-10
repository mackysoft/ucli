namespace MackySoft.Ucli.Tests.Helpers.Process;

internal sealed class RecordingUnityLogReader : IUnityLogReader
{
    private readonly List<Invocation> invocations = [];

    public RecordingUnityLogReader ()
        : this(UnityLogReadResult.Success(string.Empty, truncated: false, path: "/tmp/unity.log", sizeBytes: 0))
    {
    }

    public RecordingUnityLogReader (UnityLogReadResult result)
    {
        NextResult = result;
    }

    public UnityLogReadResult NextResult { get; set; }

    public Func<string, string, int, CancellationToken, ValueTask<UnityLogReadResult>>? ReadAsyncHandler { get; set; }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<UnityLogReadResult> ReadTailAsync (
        string storageRoot,
        string projectFingerprint,
        int maxBytes = IUnityLogReader.DefaultMaxBytes,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        invocations.Add(new Invocation(storageRoot, projectFingerprint, maxBytes, cancellationToken));
        if (ReadAsyncHandler is not null)
        {
            return ReadAsyncHandler(storageRoot, projectFingerprint, maxBytes, cancellationToken);
        }

        return ValueTask.FromResult(NextResult);
    }

    internal readonly record struct Invocation (
        string StorageRoot,
        string ProjectFingerprint,
        int MaxBytes,
        CancellationToken CancellationToken);
}
