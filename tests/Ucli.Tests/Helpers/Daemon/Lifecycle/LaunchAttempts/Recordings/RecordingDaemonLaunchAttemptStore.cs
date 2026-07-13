using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.LaunchAttempts;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal sealed class RecordingDaemonLaunchAttemptStore : IDaemonLaunchAttemptStore
{
    private readonly List<ReadInvocation> readInvocations = [];
    private readonly List<WriteInvocation> writeInvocations = [];
    private readonly List<PruneInvocation> pruneInvocations = [];

    public DaemonLaunchAttemptReadResult ReadResult { get; set; } =
        DaemonLaunchAttemptReadResult.Success(null);

    public DaemonLaunchAttemptStoreOperationResult WriteResult { get; set; } =
        DaemonLaunchAttemptStoreOperationResult.Success();

    public DaemonLaunchAttemptStoreOperationResult PruneResult { get; set; } =
        DaemonLaunchAttemptStoreOperationResult.Success();

    public Queue<DaemonLaunchAttemptStoreOperationResult> WriteResults { get; } = new();

    public Action<DaemonLaunchAttempt>? OnWrite { get; set; }

    public Func<string, ProjectFingerprint, DaemonLaunchAttempt, CancellationToken, ValueTask<DaemonLaunchAttemptStoreOperationResult>>? WriteAsyncHandler { get; set; }

    public Func<string, ProjectFingerprint, int, CancellationToken, ValueTask<DaemonLaunchAttemptStoreOperationResult>>? PruneAsyncHandler { get; set; }

    public IReadOnlyList<ReadInvocation> ReadInvocations => readInvocations;

    public IReadOnlyList<WriteInvocation> WriteInvocations => writeInvocations;

    public IReadOnlyList<PruneInvocation> PruneInvocations => pruneInvocations;

    public ValueTask<DaemonLaunchAttemptStoreOperationResult> WriteFailureAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        DaemonLaunchAttempt launchAttempt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(launchAttempt);
        cancellationToken.ThrowIfCancellationRequested();

        writeInvocations.Add(new WriteInvocation(storageRoot, projectFingerprint, launchAttempt, cancellationToken));
        OnWrite?.Invoke(launchAttempt);

        if (WriteAsyncHandler is not null)
        {
            return WriteAsyncHandler(storageRoot, projectFingerprint, launchAttempt, cancellationToken);
        }

        return ValueTask.FromResult(WriteResults.Count > 0 ? WriteResults.Dequeue() : WriteResult);
    }

    public ValueTask<DaemonLaunchAttemptReadResult> ReadLastFailureAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        readInvocations.Add(new ReadInvocation(storageRoot, projectFingerprint, cancellationToken));

        return ValueTask.FromResult(ReadResult);
    }

    public ValueTask<DaemonLaunchAttemptStoreOperationResult> PruneAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        int keepCount,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        pruneInvocations.Add(new PruneInvocation(storageRoot, projectFingerprint, keepCount, cancellationToken));

        if (PruneAsyncHandler is not null)
        {
            return PruneAsyncHandler(storageRoot, projectFingerprint, keepCount, cancellationToken);
        }

        return ValueTask.FromResult(PruneResult);
    }

    internal readonly record struct ReadInvocation (
        string StorageRoot,
        ProjectFingerprint ProjectFingerprint,
        CancellationToken CancellationToken);

    internal readonly record struct WriteInvocation (
        string StorageRoot,
        ProjectFingerprint ProjectFingerprint,
        DaemonLaunchAttempt LaunchAttempt,
        CancellationToken CancellationToken);

    internal readonly record struct PruneInvocation (
        string StorageRoot,
        ProjectFingerprint ProjectFingerprint,
        int KeepCount,
        CancellationToken CancellationToken);
}
