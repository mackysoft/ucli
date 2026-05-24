using MackySoft.Ucli.Application.Shared.Execution.ReadPostcondition;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class TestMutationReadPostconditionStore : IMutationReadPostconditionStore
{
    public int ReadCallCount { get; private set; }

    public int WriteCallCount { get; private set; }

    public string? LastStorageRoot { get; private set; }

    public string? LastProjectFingerprint { get; private set; }

    public OperationExecutionReadPostcondition? LastWrittenReadPostcondition { get; private set; }

    public MutationReadPostconditionReadResult ReadResult { get; set; }
        = MutationReadPostconditionReadResult.Success(null);

    public MutationReadPostconditionStoreOperationResult WriteResult { get; set; }
        = MutationReadPostconditionStoreOperationResult.Success();

    public ValueTask<MutationReadPostconditionReadResult> ReadOrNullAsync (
        string storageRoot,
        string projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ReadCallCount++;
        LastStorageRoot = storageRoot;
        LastProjectFingerprint = projectFingerprint;
        return ValueTask.FromResult(ReadResult);
    }

    public ValueTask<MutationReadPostconditionStoreOperationResult> WriteMergedAsync (
        string storageRoot,
        string projectFingerprint,
        OperationExecutionReadPostcondition readPostcondition,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(readPostcondition);
        WriteCallCount++;
        LastStorageRoot = storageRoot;
        LastProjectFingerprint = projectFingerprint;
        LastWrittenReadPostcondition = readPostcondition;
        return ValueTask.FromResult(WriteResult);
    }
}
