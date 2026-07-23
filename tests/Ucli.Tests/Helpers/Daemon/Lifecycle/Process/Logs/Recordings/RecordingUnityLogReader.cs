using MackySoft.FileSystem;

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

    public Func<AbsolutePath, ProjectFingerprint, int, CancellationToken, ValueTask<UnityLogReadResult>>? ReadAsyncHandler { get; set; }

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValueTask<UnityLogReadResult> ReadTailAsync (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
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
        AbsolutePath StorageRoot,
        ProjectFingerprint ProjectFingerprint,
        int MaxBytes,
        CancellationToken CancellationToken);
}
