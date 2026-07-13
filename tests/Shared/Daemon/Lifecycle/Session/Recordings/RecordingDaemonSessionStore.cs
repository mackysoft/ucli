using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.TestSupport;

internal sealed class RecordingDaemonSessionStore : IDaemonSessionStore
{
    private readonly bool readOnly;
    private readonly List<ReadInvocation> readInvocations = [];
    private readonly List<WriteInvocation> writeInvocations = [];
    private readonly List<DeleteInvocation> deleteInvocations = [];

    private DaemonSessionReadResult readResult;

    public RecordingDaemonSessionStore ()
    {
        readResult = DaemonSessionReadResult.Missing();
    }

    public RecordingDaemonSessionStore (DaemonSessionReadResult readResult)
    {
        this.readResult = readResult;
        readOnly = true;
    }

    public Action? OnRead { get; set; }

    public Func<string, ProjectFingerprint, CancellationToken, ValueTask<DaemonSessionReadResult>>? ReadAsyncHandler { get; set; }

    public Func<IReadOnlyList<ReadInvocation>, DaemonSessionReadResult>? ReadHandler { get; set; }

    public Exception? ReadException { get; set; }

    public DaemonSessionReadResult ReadResult
    {
        get => readResult;
        set => readResult = value;
    }

    public DaemonSessionStoreOperationResult WriteResult { get; set; } =
        DaemonSessionStoreOperationResult.Success();

    public DaemonSessionStoreOperationResult DeleteResult { get; set; } =
        DaemonSessionStoreOperationResult.Success();

    public IReadOnlyList<ReadInvocation> ReadInvocations => readInvocations;

    public IReadOnlyList<WriteInvocation> WriteInvocations => writeInvocations;

    public IReadOnlyList<DeleteInvocation> DeleteInvocations => deleteInvocations;

    public ValueTask<DaemonSessionReadResult> ReadAsync (
        string storageRoot,
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

        if (ReadException is not null)
        {
            return ValueTask.FromException<DaemonSessionReadResult>(ReadException);
        }

        return ValueTask.FromResult(ReadHandler?.Invoke(readInvocations) ?? readResult);
    }

    public ValueTask<DaemonSessionStoreOperationResult> WriteAsync (
        string storageRoot,
        DaemonSession session,
        CancellationToken cancellationToken = default)
    {
        if (readOnly)
        {
            throw new NotSupportedException("This test session store supports read operations only.");
        }

        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();

        writeInvocations.Add(new WriteInvocation(storageRoot, session, cancellationToken));

        return ValueTask.FromResult(WriteResult);
    }

    public ValueTask<DaemonSessionStoreOperationResult> DeleteAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        if (readOnly)
        {
            throw new NotSupportedException("This test session store supports read operations only.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        deleteInvocations.Add(new DeleteInvocation(storageRoot, projectFingerprint, cancellationToken));

        return ValueTask.FromResult(DeleteResult);
    }

    internal readonly record struct ReadInvocation (
        string StorageRoot,
        ProjectFingerprint ProjectFingerprint,
        CancellationToken CancellationToken);

    internal readonly record struct WriteInvocation (
        string StorageRoot,
        DaemonSession Session,
        CancellationToken CancellationToken);

    internal readonly record struct DeleteInvocation (
        string StorageRoot,
        ProjectFingerprint ProjectFingerprint,
        CancellationToken CancellationToken);
}
