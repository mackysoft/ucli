using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;

namespace MackySoft.Ucli.TestSupport;

internal sealed class RecordingDaemonDiagnosisStore : IDaemonDiagnosisStore
{
    private readonly List<DeleteInvocation> deleteInvocations = [];
    private readonly List<ReadInvocation> readInvocations = [];
    private readonly List<WriteInvocation> writeInvocations = [];

    public Func<AbsolutePath, ProjectFingerprint, DaemonDiagnosisReadResult>? OnRead { get; set; }

    public Action<DaemonDiagnosis>? OnWrite { get; set; }

    public DaemonDiagnosisReadResult ReadResult { get; set; } =
        DaemonDiagnosisReadResult.Success(null);

    public DaemonDiagnosisStoreOperationResult WriteResult { get; set; } =
        DaemonDiagnosisStoreOperationResult.Success();

    public DaemonDiagnosisStoreOperationResult DeleteResult { get; set; } =
        DaemonDiagnosisStoreOperationResult.Success();

    public Func<AbsolutePath, ProjectFingerprint, CancellationToken, ValueTask<DaemonDiagnosisStoreOperationResult>>? DeleteAsyncHandler { get; set; }

    public Func<AbsolutePath, ProjectFingerprint, DaemonDiagnosis, CancellationToken, ValueTask<DaemonDiagnosisStoreOperationResult>>? WriteAsyncHandler { get; set; }

    public IReadOnlyList<ReadInvocation> ReadInvocations => readInvocations;

    public IReadOnlyList<WriteInvocation> WriteInvocations => writeInvocations;

    public IReadOnlyList<DeleteInvocation> DeleteInvocations => deleteInvocations;

    public ValueTask<DaemonDiagnosisReadResult> ReadAsync (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        readInvocations.Add(new ReadInvocation(storageRoot, projectFingerprint, cancellationToken));

        return ValueTask.FromResult(OnRead?.Invoke(storageRoot, projectFingerprint) ?? ReadResult);
    }

    public ValueTask<DaemonDiagnosisStoreOperationResult> WriteAsync (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        DaemonDiagnosis diagnosis,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(diagnosis);
        cancellationToken.ThrowIfCancellationRequested();

        writeInvocations.Add(new WriteInvocation(storageRoot, projectFingerprint, diagnosis, cancellationToken));
        OnWrite?.Invoke(diagnosis);

        if (WriteAsyncHandler is not null)
        {
            return WriteAsyncHandler(storageRoot, projectFingerprint, diagnosis, cancellationToken);
        }

        return ValueTask.FromResult(WriteResult);
    }

    public ValueTask<DaemonDiagnosisStoreOperationResult> DeleteAsync (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        deleteInvocations.Add(new DeleteInvocation(storageRoot, projectFingerprint, cancellationToken));

        if (DeleteAsyncHandler is not null)
        {
            return DeleteAsyncHandler(storageRoot, projectFingerprint, cancellationToken);
        }

        return ValueTask.FromResult(DeleteResult);
    }

    internal readonly record struct ReadInvocation (
        AbsolutePath StorageRoot,
        ProjectFingerprint ProjectFingerprint,
        CancellationToken CancellationToken);

    internal readonly record struct WriteInvocation (
        AbsolutePath StorageRoot,
        ProjectFingerprint ProjectFingerprint,
        DaemonDiagnosis Diagnosis,
        CancellationToken CancellationToken);

    internal readonly record struct DeleteInvocation (
        AbsolutePath StorageRoot,
        ProjectFingerprint ProjectFingerprint,
        CancellationToken CancellationToken);
}
