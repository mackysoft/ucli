using MackySoft.FileSystem;
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

    public Action? OnRead { get; set; }

    public Func<AbsolutePath, ProjectFingerprint, CancellationToken, ValueTask<DaemonLifecycleObservationReadResult>>? ReadAsyncHandler { get; set; }

    public ValueTask<DaemonLifecycleObservationReadResult> ReadAsync (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        readInvocations.Add(new ReadInvocation(storageRoot, projectFingerprint, cancellationToken));
        OnRead?.Invoke();
        if (ReadAsyncHandler is not null)
        {
            return ReadAsyncHandler(storageRoot, projectFingerprint, cancellationToken);
        }

        return ValueTask.FromResult(ReadResult);
    }

    public ValueTask<DaemonLifecycleStoreOperationResult> DeleteAsync (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        deleteInvocations.Add(new DeleteInvocation(storageRoot, projectFingerprint, cancellationToken));

        return ValueTask.FromResult(DeleteResult);
    }

    internal readonly record struct ReadInvocation (
        AbsolutePath StorageRoot,
        ProjectFingerprint ProjectFingerprint,
        CancellationToken CancellationToken);

    internal readonly record struct DeleteInvocation (
        AbsolutePath StorageRoot,
        ProjectFingerprint ProjectFingerprint,
        CancellationToken CancellationToken);
}
