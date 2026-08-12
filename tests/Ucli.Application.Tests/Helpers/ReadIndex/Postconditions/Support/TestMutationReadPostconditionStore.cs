using MackySoft.Ucli.Application.Shared.Execution.ReadPostcondition;
using MackySoft.Ucli.Contracts.Execution;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class TestMutationReadPostconditionStore : IMutationReadPostconditionStore
{
    private readonly List<ReadInvocation> readInvocations = [];
    private readonly List<WriteInvocation> writeInvocations = [];
    private readonly List<InvalidationInvocation> invalidationInvocations = [];

    public MutationReadPostconditionReadResult ReadResult { get; set; }
        = MutationReadPostconditionReadResult.Success(null);

    public MutationReadPostconditionStoreOperationResult WriteResult { get; set; }
        = MutationReadPostconditionStoreOperationResult.Success();

    public MutationReadPostconditionStoreOperationResult InvalidationResult { get; set; }
        = MutationReadPostconditionStoreOperationResult.Success();

    public IReadOnlyList<ReadInvocation> ReadInvocations => readInvocations;

    public IReadOnlyList<WriteInvocation> WriteInvocations => writeInvocations;

    public IReadOnlyList<InvalidationInvocation> InvalidationInvocations => invalidationInvocations;

    public ValueTask<MutationReadPostconditionReadResult> ReadOrNullAsync (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        readInvocations.Add(new ReadInvocation(storageRoot, projectFingerprint, cancellationToken));
        return ValueTask.FromResult(ReadResult);
    }

    public ValueTask<MutationReadPostconditionStoreOperationResult> WriteMergedAsync (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        ExecutionReadPostcondition readPostcondition,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(readPostcondition);
        writeInvocations.Add(new WriteInvocation(storageRoot, projectFingerprint, readPostcondition, cancellationToken));
        return ValueTask.FromResult(WriteResult);
    }

    public ValueTask<MutationReadPostconditionStoreOperationResult> InvalidateAfterUnobservedEvalCallAsync (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        invalidationInvocations.Add(new InvalidationInvocation(storageRoot, projectFingerprint, cancellationToken));
        return ValueTask.FromResult(InvalidationResult);
    }

    internal readonly record struct ReadInvocation (
        AbsolutePath StorageRoot,
        ProjectFingerprint ProjectFingerprint,
        CancellationToken CancellationToken);

    internal readonly record struct WriteInvocation (
        AbsolutePath StorageRoot,
        ProjectFingerprint ProjectFingerprint,
        ExecutionReadPostcondition ReadPostcondition,
        CancellationToken CancellationToken);

    internal readonly record struct InvalidationInvocation (
        AbsolutePath StorageRoot,
        ProjectFingerprint ProjectFingerprint,
        CancellationToken CancellationToken);
}
