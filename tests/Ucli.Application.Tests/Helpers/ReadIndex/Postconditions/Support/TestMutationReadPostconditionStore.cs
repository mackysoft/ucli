using MackySoft.Ucli.Application.Shared.Execution.ReadPostcondition;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class TestMutationReadPostconditionStore : IMutationReadPostconditionStore
{
    private readonly List<ReadInvocation> readInvocations = [];
    private readonly List<WriteInvocation> writeInvocations = [];

    public MutationReadPostconditionReadResult ReadResult { get; set; }
        = MutationReadPostconditionReadResult.Success(null);

    public MutationReadPostconditionStoreOperationResult WriteResult { get; set; }
        = MutationReadPostconditionStoreOperationResult.Success();

    public IReadOnlyList<ReadInvocation> ReadInvocations => readInvocations;

    public IReadOnlyList<WriteInvocation> WriteInvocations => writeInvocations;

    public ValueTask<MutationReadPostconditionReadResult> ReadOrNullAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        readInvocations.Add(new ReadInvocation(storageRoot, projectFingerprint, cancellationToken));
        return ValueTask.FromResult(ReadResult);
    }

    public ValueTask<MutationReadPostconditionStoreOperationResult> WriteMergedAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        OperationExecutionReadPostcondition readPostcondition,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(readPostcondition);
        writeInvocations.Add(new WriteInvocation(storageRoot, projectFingerprint, readPostcondition, cancellationToken));
        return ValueTask.FromResult(WriteResult);
    }

    internal readonly record struct ReadInvocation (
        string StorageRoot,
        ProjectFingerprint ProjectFingerprint,
        CancellationToken CancellationToken);

    internal readonly record struct WriteInvocation (
        string StorageRoot,
        ProjectFingerprint ProjectFingerprint,
        OperationExecutionReadPostcondition ReadPostcondition,
        CancellationToken CancellationToken);
}
