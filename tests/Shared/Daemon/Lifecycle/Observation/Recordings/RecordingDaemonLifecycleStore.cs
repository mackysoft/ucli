using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;

namespace MackySoft.Ucli.TestSupport;

internal sealed class RecordingDaemonLifecycleStore : IDaemonLifecycleStore
{
    private readonly List<ReadInvocation> readInvocations = [];
    private readonly List<DeleteInvocation> deleteInvocations = [];

    public DaemonLifecycleObservationReadResult ReadResult { get; set; } =
        DaemonLifecycleObservationReadResult.Success(null);

    public DaemonLifecycleStoreOperationResult DeleteResult { get; set; } =
        DaemonLifecycleStoreOperationResult.Success();

    public IReadOnlyList<ReadInvocation> ReadInvocations => readInvocations;

    public IReadOnlyList<DeleteInvocation> DeleteInvocations => deleteInvocations;

    public ValueTask<DaemonLifecycleObservationReadResult> ReadAsync (
        string storageRoot,
        string projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        readInvocations.Add(new ReadInvocation(storageRoot, projectFingerprint, cancellationToken));

        return ValueTask.FromResult(ReadResult);
    }

    public ValueTask<DaemonLifecycleStoreOperationResult> DeleteAsync (
        string storageRoot,
        string projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        deleteInvocations.Add(new DeleteInvocation(storageRoot, projectFingerprint, cancellationToken));

        return ValueTask.FromResult(DeleteResult);
    }

    internal readonly record struct ReadInvocation (
        string StorageRoot,
        string ProjectFingerprint,
        CancellationToken CancellationToken);

    internal readonly record struct DeleteInvocation (
        string StorageRoot,
        string ProjectFingerprint,
        CancellationToken CancellationToken);
}
